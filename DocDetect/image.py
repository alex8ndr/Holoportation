import socket
import struct
import time

# Server details
SERVER_HOST = '127.0.0.1'
SERVER_PORT_RECEIVE = 48003

def send_image_via_tcp(image_path):
    try:
        with open(image_path, 'rb') as image_file:
            image_data = image_file.read()

        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client_socket:
            client_socket.connect((SERVER_HOST, SERVER_PORT_RECEIVE))
            print(f"Connected to server at {SERVER_HOST}:{SERVER_PORT_RECEIVE}")

            while True:
                start_time = time.time()

                # Send the size of the image first
                image_size = struct.pack("L", len(image_data))
                client_socket.sendall(image_size)

                # Send the image data
                client_socket.sendall(image_data)
                print("Sent image")

                # Wait to maintain 30fps
                elapsed_time = time.time() - start_time
                time.sleep(max(1./30 - elapsed_time, 0))

    except Exception as e:
        print(f"Error sending image: {e}")

# Send the image repeatedly at 30fps
send_image_via_tcp('image.jpg')
