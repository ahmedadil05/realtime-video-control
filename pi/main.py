import cv2
import socket
import time
import struct
import threading

# ==========================================
#     CONFIGURATION
# ==========================================
PC_IP = "100.93.237.108"  # CHANGE THIS to your PC's Tailscale IP
VIDEO_PORT = 5001
CONTROL_PORT = 5000
RESOLUTION = (640, 480)
JPEG_QUALITY = 50
# Use 0 for default camera. If it fails, you might need to find the correct index or use a specific library like picamera2
CAMERA_INDEX = 0 

# ==========================================
#     STATE & CONTROL
# ==========================================
# A threading.Event is a safe way to share a boolean flag between threads
streaming_active = threading.Event()

# ==========================================
#     CONTROL SERVER (TCP)
# ==========================================
def control_server():
    """
    Listens for TCP commands to start/stop the stream.
    Runs in a separate thread.
    """
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock.bind(("0.0.0.0", CONTROL_PORT))
        sock.listen(1)
        print(f"Control server listening on port {CONTROL_PORT}")

        while True:
            try:
                conn, addr = sock.accept()
                with conn:
                    print(f"Controller connected from {addr}")
                    while True:
                        data = conn.recv(1024)
                        if not data:
                            break
                        
                        command = data.decode('utf-8').strip().upper()
                        print(f"Received command: {command}")

                        if command == "START":
                            streaming_active.set() # Set the event to True
                            conn.sendall(b"OK: STREAMING STARTED\n")
                        elif command == "STOP":
                            streaming_active.clear() # Set the event to False
                            conn.sendall(b"OK: STREAMING STOPPED\n")
                        else:
                            conn.sendall(b"ERR: UNKNOWN COMMAND\n")
                print(f"Controller disconnected.")

            except Exception as e:
                print(f"Control server error: {e}")
                time.sleep(1) # Avoid busy-looping on error

# ==========================================
#     VIDEO STREAMER (UDP)
# ==========================================
def stream_video():
    """
    Captures video, encodes it, and streams it over UDP when streaming_active is True.
    """
    # Protocol: SEQ (uint32), TIMESTAMP (uint64), JPEG_BYTES
    packet_format = struct.Struct("!I Q") 

    print("Initializing camera...")
    cap = cv2.VideoCapture(CAMERA_INDEX)
    if not cap.isOpened():
        print(f"Error: Could not open camera at index {CAMERA_INDEX}.")
        print("Please check camera connection and permissions.")
        return

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, RESOLUTION[0])
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, RESOLUTION[1])
    
    print(f"Camera opened. Resolution: {cap.get(cv2.CAP_PROP_FRAME_WIDTH)}x{cap.get(cv2.CAP_PROP_FRAME_HEIGHT)}")

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        seq = 0
        print(f"Waiting for START command...")
        
        while True:
            streaming_active.wait() # This will block until the event is set (START command is received)

            ret, frame = cap.read()
            if not ret:
                print("Error: Failed to grab frame.")
                time.sleep(0.5)
                continue

            # No need to resize if CAP_PROP is set correctly, but we can enforce it.
            # frame = cv2.resize(frame, RESOLUTION) 

            # Encode to JPEG
            result, jpeg = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), JPEG_QUALITY])
            if not result:
                print("Error: JPEG encoding failed.")
                continue

            # Get timestamp (nanoseconds for high precision)
            timestamp_ns = time.time_ns()

            # Pack header and append payload
            header = packet_format.pack(seq, timestamp_ns)
            packet = header + jpeg.tobytes()

            # Check if packet is too large for standard MTU
            if len(packet) > 65507: # Max UDP payload size
                print(f"Warning: Frame {seq} is too large ({len(packet)} bytes). Consider lowering JPEG_QUALITY.")
                continue

            sock.sendto(packet, (PC_IP, VIDEO_PORT))

            seq += 1
            
            # If STOP command is received, this loop will break
            if not streaming_active.is_set():
                print("STOP command received. Pausing stream.")
                # The outer loop will then block on streaming_active.wait()

# ==========================================
#     MAIN
# ==========================================
if __name__ == "__main__":
    # Start the control server in a background thread
    control_thread = threading.Thread(target=control_server, daemon=True)
    control_thread.start()

    # Start the video streamer in the main thread
    stream_video()
