import cv2
import numpy as np
from datetime import datetime
import os
import socket
import torch
from ultralytics import YOLO
import struct
import time
import threading
from queue import Queue, Full
import sys
import signal
from pathlib import Path

# Configuration Constants
SERVER_HOST = '127.0.0.1'
SERVER_PORTS = {'receive': 48005, 'send': 48006}  # Using different ports for UDP
DETECTION_CLASSES = [
    "flat_or_crinkled_paper_document_with_text_and_or_image_and_or_writing",
]
FRAME_QUEUE_SIZE = 1
MIN_SCREENSHOT_INTERVAL = 10  # Seconds
SHUTDOWN_TIMEOUT = 2  # Seconds for graceful shutdown
MAX_UDP_PACKET_SIZE = 65507  # Max UDP packet size

class DocumentDetector:
    def __init__(self):
        self.running = True
        self.frame_queue = Queue(maxsize=FRAME_QUEUE_SIZE)
        self.model = None
        self.model_ready = threading.Event()
        self.blur_scores = {}
        self.output_dir = Path("screenshots")
        self.output_dir.mkdir(exist_ok=True)
        self.server_socket = None
        self.processing_thread = None
        self.save_mode = "--save" in sys.argv
        
        signal.signal(signal.SIGINT, self.shutdown_handler)
        signal.signal(signal.SIGTERM, self.shutdown_handler)
        self.start_model_loading()

    def start_model_loading(self):
        def load_model():
            try:
                self.model = YOLO('yolov8s-worldv2.pt')
                device = 'cuda' if torch.cuda.is_available() else 'cpu'
                self.model.to(device)
                print(f"Using device: {device}")
                if device == 'cuda':
                    print(f"CUDA Device: {torch.cuda.get_device_name(0)}")
                self.model.set_classes(DETECTION_CLASSES)
            except Exception as e:
                print(f"Model loading error: {e}")
                sys.exit(1)
            finally:
                self.model_ready.set()
            
        threading.Thread(target=load_model, daemon=True).start()

    def shutdown_handler(self, signum, frame):
        if not self.running:
            return
            
        print("\nInitiating graceful shutdown...")
        self.running = False
        
        # Close server socket to unblock accept()
        if self.server_socket:
            try:
                self.server_socket.close()
            except Exception as e:
                print(f"Server socket close error: {e}")

        # Clear frame queue and signal processor
        while not self.frame_queue.empty():
            self.frame_queue.get()
        self.frame_queue.put(None)

        # Wait for processing thread
        if self.processing_thread and self.processing_thread.is_alive():
            self.processing_thread.join(SHUTDOWN_TIMEOUT)

        print("Shutdown complete.")
        sys.exit(0)

    def process_frame(self, frame):
        self.model_ready.wait()
        return self.model.predict(
            frame,
            conf=0.10,
            iou=0.45,
            max_det=1
        )

    def handle_detection(self, frame, results):
        current_time = time.time()
        show_display = "--show" in sys.argv
        
        for i, box in enumerate(results[0].boxes):
            x1, y1, x2, y2 = map(int, box.xyxy[0])
            label = self.model.names[int(box.cls[0])]
            
            roi = frame[y1:y2, x1:x2]
            self._process_roi(roi, label, i, current_time)

            if show_display:
                # Only draw boxes if display is enabled
                cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
                cv2.putText(frame, f"{label}: {box.conf[0]:.2f}", 
                           (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)

    def _process_roi(self, roi, label, idx, current_time):
        gray_roi = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
        laplacian_var = cv2.Laplacian(gray_roi, cv2.CV_64F).var()
        detection_key = f"{label}_{idx}"
        
        # Debug print for blur score
        print(f"[{datetime.now().strftime('%H:%M:%S')}] ROI processing: {detection_key}, Blur score: {laplacian_var:.2f}, Last score: {self.blur_scores.get(detection_key, 0):.2f}")
        
        should_capture = self._should_capture(detection_key, laplacian_var, current_time)
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Should capture: {should_capture}")
        
        if should_capture:
            self._save_and_send_roi(roi, detection_key, laplacian_var)
            self.blur_scores[detection_key] = laplacian_var

    def _should_capture(self, key, score, timestamp):
        last_capture_time = self.blur_scores.get('last_capture', 0)
        time_since_capture = timestamp - last_capture_time
        
        better_blur = key in self.blur_scores and score > self.blur_scores[key] * 1.1
        no_previous = key not in self.blur_scores
        enough_time = time_since_capture >= MIN_SCREENSHOT_INTERVAL
        
        # Debug print for capture decision logic
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Capture decision for {key}:")
        print(f"  - Time since last capture: {time_since_capture:.2f}s (threshold: {MIN_SCREENSHOT_INTERVAL}s)")
        print(f"  - Better blur: {better_blur} (Current: {score:.2f}, Previous: {self.blur_scores.get(key, 0):.2f})")
        print(f"  - No previous: {no_previous}")
        print(f"  - Enough time passed: {enough_time}")
        
        return no_previous or better_blur or enough_time

    def _save_and_send_roi(self, roi, key, score):
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        path = self.output_dir / f"{key}_{timestamp}.png"
        
        if self.save_mode:
            cv2.imwrite(str(path), roi)
            print(f"[{datetime.now().strftime('%H:%M:%S')}] CAPTURED: {path} (Blur: {score:.2f})")
        else:
            # Create a temporary in-memory file
            is_success, buffer = cv2.imencode(".png", roi)
            if not is_success:
                print(f"Error encoding image to PNG format")
                return
            
            # Use a temporary file if not saving
            path = self.output_dir / f"{key}_{timestamp}.png"
            print(f"[{datetime.now().strftime('%H:%M:%S')}] CAPTURED (not saved): {key} (Blur: {score:.2f})")
        
        threading.Thread(target=self.send_image, args=(path, roi), daemon=True).start()
        self.blur_scores[key] = score
        self.blur_scores['last_capture'] = time.time()
        print(f"[{datetime.now().strftime('%H:%M:%S')}] Updated last_capture timestamp: {self.blur_scores['last_capture']}")

    def send_image(self, path, roi=None):
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                # Get image data - either from file or directly from memory
                if self.save_mode:
                    # Read image from file
                    with open(path, 'rb') as f:
                        data = f.read()

                    # Get image dimensions
                    img = cv2.imread(str(path))
                    if img is None:
                        print(f"Error: Could not read image {path}")
                        return
                else:
                    # Use the ROI directly if not saving
                    img = roi
                    is_success, data = cv2.imencode(".png", roi)
                    if not is_success:
                        print("Error encoding image")
                        return
                    data = data.tobytes()

                height, width, _ = img.shape

                # For UDP we need to handle potential fragmentation
                # Send header first
                header = struct.pack("II", height, width)
                s.sendto(header, (SERVER_HOST, SERVER_PORTS['send']))
                
                # Send the data size
                s.sendto(struct.pack("L", len(data)), (SERVER_HOST, SERVER_PORTS['send']))
                
                # Split data into chunks to avoid UDP packet size limits
                chunk_size = MAX_UDP_PACKET_SIZE - 100  # Leave room for headers
                chunks = [data[i:i+chunk_size] for i in range(0, len(data), chunk_size)]
                
                # Send total number of chunks
                s.sendto(struct.pack("I", len(chunks)), (SERVER_HOST, SERVER_PORTS['send']))
                
                # Send each chunk with index
                for i, chunk in enumerate(chunks):
                    # Format: chunk index, total chunks, chunk data
                    packet = struct.pack("I", i) + chunk
                    s.sendto(packet, (SERVER_HOST, SERVER_PORTS['send']))
                    time.sleep(0.001)  # Small delay to prevent network congestion

                status = "saved" if self.save_mode else "not saved"
                print(f"[{datetime.now().strftime('%H:%M:%S')}] Image sent in {len(chunks)} chunks ({status})")

        except Exception as e:
            print(f"Send error: {e}")

    def frame_processor(self):
        last_time = time.time()
        frame_count = 0
        show_display = "--show" in sys.argv
        
        while self.running:
            frame = self.frame_queue.get()
            if frame is None:
                break
                
            frame_count += 1
            if frame_count % 20 == 0:  # Only print every 20 frames to reduce console spam
                print(f"[{datetime.now().strftime('%H:%M:%S')}] Processing frame {frame_count}, Queue size: {self.frame_queue.qsize()}/{FRAME_QUEUE_SIZE}")

            results = self.process_frame(frame)
            self.handle_detection(frame, results)
            
            if show_display:
                # Only calculate FPS and draw UI elements if display is enabled
                fps = 1 / (time.time() - last_time)
                cv2.putText(frame, f"FPS: {fps:.2f}", (10, 30), 
                          cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                # Resize frame for display to 720p
                display_height = 720
                display_width = 1280
                display_frame = cv2.resize(frame, (display_width, display_height))
                
                cv2.imshow("Document Detection", display_frame)
                cv2.waitKey(1)
                
                # Set window size on first frame
                if frame_count == 1:
                    cv2.namedWindow("Document Detection", cv2.WINDOW_NORMAL)
                    cv2.resizeWindow("Document Detection", display_width, display_height)
            
            last_time = time.time()

        if show_display:
            cv2.destroyAllWindows()

    def udp_receiver(self):
        # Creating UDP socket
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.server_socket.bind((SERVER_HOST, SERVER_PORTS['receive']))
        self.server_socket.settimeout(1.0)  # 1 second timeout for clean shutdown
        print(f"UDP server listening on {SERVER_HOST}:{SERVER_PORTS['receive']}")
        
        # Buffer for fragments
        fragments = {}
        current_frame_id = 0
        expected_fragments = 0
        
        while self.running:
            try:
                data, addr = self.server_socket.recvfrom(MAX_UDP_PACKET_SIZE)
                
                # Check packet type
                if len(data) < 8:  # At least need frame_id and fragment_id
                    continue
                    
                frame_id, fragment_id, total_fragments = struct.unpack("III", data[:12])
                fragment_data = data[12:]
                
                # New frame started
                if frame_id != current_frame_id:
                    fragments = {}
                    current_frame_id = frame_id
                    expected_fragments = total_fragments
                
                # Store the fragment
                fragments[fragment_id] = fragment_data
                
                # Check if we have all fragments
                if len(fragments) == expected_fragments:
                    # Reassemble the frame
                    # Sort by fragment_id to ensure correct order
                    all_data = b''.join([fragments[i] for i in sorted(fragments.keys())])
                    
                    # Decode the image
                    frame = cv2.imdecode(np.frombuffer(all_data, np.uint8), cv2.IMREAD_COLOR)
                    if frame is not None:
                        try:
                            self.frame_queue.put_nowait(frame)
                        except Full:
                            self.frame_queue.get()
                            self.frame_queue.put(frame)
                    
                    # Reset for next frame
                    fragments = {}
                    current_frame_id = frame_id
                    
            except socket.timeout:
                continue
            except Exception as e:
                if self.running:
                    print(f"Error in UDP receiver: {e}")

    def start_server(self):
        # Start processing thread first
        self.processing_thread = threading.Thread(target=self.frame_processor, daemon=True)
        self.processing_thread.start()
        
        # Start receiver thread
        receiver_thread = threading.Thread(target=self.udp_receiver, daemon=True)
        receiver_thread.start()
        
        print(f"Server running. Receiving on port {SERVER_PORTS['receive']}, sending on port {SERVER_PORTS['send']}")
        print("Press Ctrl+C to exit")
        
        # Keep main thread alive
        try:
            while self.running:
                time.sleep(0.1)
        except KeyboardInterrupt:
            print("\nKeyboard interrupt received, shutting down...")
            self.shutdown_handler(None, None)

if __name__ == "__main__":
    detector = DocumentDetector()
    detector.start_server()
