import socket

# "0.0.0.0" allows listening on all interfaces (Tailscale, WiFi, etc.)
HOST = "0.0.0.0" 
PORT = 5000

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
# REUSEADDR allows you to restart the script immediately without "Address already in use" errors
sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
sock.bind((HOST, PORT))
sock.listen(1)

print(f"Control server listening on {HOST}:{PORT}...")

while True:
    try:
        # Wait for a connection (Blocking)
        print("Waiting for controller...")
        conn, addr = sock.accept()
        print(f"Connected from {addr}")

        with conn:
            while True:
                data = conn.recv(1024)
                if not data:
                    print("Client disconnected.")
                    break

                command = data.decode().strip()
                print(f"Received Command: {command}")

                if command == "START":
                    conn.sendall(b"OK STARTING\n")
                    # Add logic here to trigger your video stream if needed
                elif command == "STOP":
                    conn.sendall(b"OK STOPPING\n")
                else:
                    conn.sendall(b"UNKNOWN COMMAND\n")
                    
    except Exception as e:
        print(f"Error: {e}")