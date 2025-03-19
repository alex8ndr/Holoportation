import cv2
import socket
import struct
import threading
import time
import numpy as np
import sys

# Server details
SERVER_HOST = '127.0.0.1'
SERVER_PORT_RECEIVE = 48005  # UDP port
MAX_UDP_PACKET_SIZE = 65507  # Maximum UDP packet size
FRAGMENT_SIZE = 40000  # Size of each fragment (adjust based on your network)

# Shared flag to signal threads to stop
stop_flag = threading.Event()
frame_id = 0  # Global counter for frames

def send_frame_via_udp(client_socket, frame):
    global frame_id
    
    try:
        # Compress the frame with reduced quality for UDP
        encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 80]
        _, frame_encoded = cv2.imencode('.jpg', frame, encode_param)
        frame_data = frame_encoded.tobytes()
        
        # Split the frame data into fragments
        fragments = [frame_data[i:i+FRAGMENT_SIZE] for i in range(0, len(frame_data), FRAGMENT_SIZE)]
        total_fragments = len(fragments)
        
        # Send each fragment
        for i, fragment in enumerate(fragments):
            # Packet format: frame_id (4 bytes) + fragment_id (4 bytes) + total_fragments (4 bytes) + data
            header = struct.pack("III", frame_id, i, total_fragments)
            packet = header + fragment
            client_socket.sendto(packet, (SERVER_HOST, SERVER_PORT_RECEIVE))
            
            # Small delay to prevent network congestion
            time.sleep(0.0001)
        
        frame_id += 1  # Increment frame ID for the next frame
        if frame_id % 30 == 0:  # Print status every 30 frames
            print(f"Sent frame {frame_id} in {total_fragments} fragments")
            
    except Exception as e:
        print(f"Error sending frame: {e}")

def capture_and_send_frames():
    # Create UDP socket
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    
    # Optional: Set socket buffer size (may require admin privileges)
    # client_socket.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 65536)
    
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Error: Could not open webcam.")
        return

    # Try to set the resolution (may not work on all webcams)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    
    # Adjust frame rate based on command line arguments
    frame_rate = 30
    if len(sys.argv) > 1:
        try:
            frame_rate = int(sys.argv[1])
            print(f"Setting frame rate to {frame_rate} fps")
        except ValueError:
            print(f"Invalid frame rate argument: {sys.argv[1]}, using default {frame_rate} fps")
    
    prev = 0

    try:
        print(f"Starting UDP stream to {SERVER_HOST}:{SERVER_PORT_RECEIVE}")
        
        while not stop_flag.is_set():
            time_elapsed = time.time() - prev
            if time_elapsed > 1./frame_rate:
                prev = time.time()
                ret, frame = cap.read()
                if not ret:
                    print("Error: Could not read frame.")
                    break

                try:
                    send_frame_via_udp(client_socket, frame)
                except Exception as e:
                    print(f"Error: {e}")
                    break

                # Display the frame locally
                #cv2.imshow("Webcam (UDP)", frame)
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    stop_flag.set()
                    break

    except KeyboardInterrupt:
        print("Stopping capture...")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        cap.release()
        client_socket.close()
        #cv2.destroyAllWindows()
        print("UDP client stopped")

if __name__ == "__main__":
    # Start capturing and sending frames
    try:
        capture_and_send_frames()
    except KeyboardInterrupt:
        stop_flag.set()
        print("Exiting...")
