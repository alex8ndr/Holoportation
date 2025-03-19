import cv2
import socket
import struct
import threading
import time

# Server details
SERVER_HOST = '127.0.0.1'
SERVER_PORT_RECEIVE = 48003

# Shared flag to signal threads to stop
stop_flag = threading.Event()

def send_frame_via_tcp(client_socket, frame):
    try:
        # Encode the frame
        _, frame_encoded = cv2.imencode('.jpg', frame)
        frame_data = frame_encoded.tobytes()

        # Send the size of the frame first
        frame_size = struct.pack("L", len(frame_data))
        client_socket.sendall(frame_size)

        # Send the frame data
        client_socket.sendall(frame_data)
        print("Sent frame")
    except Exception as e:
        print(f"Error sending frame: {e}")

def capture_and_send_frames():
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Error: Could not open webcam.")
        return

    # Attempt to set the resolution to 4K
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)
    
    # Set the frame rate to 30 fps
    frame_rate = 30
    prev = 0

    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client_socket:
            client_socket.connect((SERVER_HOST, SERVER_PORT_RECEIVE))
            print(f"Connected to server at {SERVER_HOST}:{SERVER_PORT_RECEIVE}")

            while not stop_flag.is_set():
                time_elapsed = time.time() - prev
                if time_elapsed > 1./frame_rate:
                    prev = time.time()
                    ret, frame = cap.read()
                    if not ret:
                        print("Error: Could not read frame.")
                        break

                    try:
                        send_frame_via_tcp(client_socket, frame)
                    except MemoryError:
                        print("Error: Insufficient memory to send frame.")
                        break

                    cv2.imshow("Webcam", frame)
                    if cv2.waitKey(1) & 0xFF == ord('q'):
                        stop_flag.set()
                        break

    except Exception as e:
        print(f"Error: {e}")

    cap.release()
    cv2.destroyAllWindows()

# Start capturing and sending frames
capture_and_send_frames()
