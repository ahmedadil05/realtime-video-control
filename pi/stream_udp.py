import socket
import cv2
import math
import struct
from protocol import pack_packet

# ==========================================
#     CONFIGURATION 
# ==========================================
# I kept PC IP, but changed port back to 5000 to match the PC Receiver.
ADDR = ("100.93.237.108", 5000) 

# ==========================================
# CAMERA SETUP (The Fix)
# ==========================================
# We MUST use this pipeline instead of '0' or it will crash on Pi 4.
pipeline = (
    "libcamerasrc ! "
    "video/x-raw,width=640,height=480,framerate=30/1 ! "
    "videoconvert ! "
    "video/x-raw,format=BGR ! "
    "appsink"
)

print(f"Opening camera...\n{pipeline}")
cap = cv2.VideoCapture(pipeline, cv2.CAP_GSTREAMER)

if not cap.isOpened():
    print("Error: Could not open camera.")
    exit(1)

# ==========================================
# UDP SETUP
# ==========================================
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
MAX_PAYLOAD_SIZE = 1024
frame_id = 0

print(f"Streaming video to {ADDR}...")

try:
    while True:
        ret, frame = cap.read()
        if not ret: break

        # Encode to JPEG
        _, encoded = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 50])
        data = encoded.tobytes()

        # Calculate Chunks
        file_size = len(data)
        total_chunks = math.ceil(file_size / MAX_PAYLOAD_SIZE)

        # Send Chunks
        for chunk_id in range(total_chunks):
            start = chunk_id * MAX_PAYLOAD_SIZE
            end = min(start + MAX_PAYLOAD_SIZE, file_size)
            payload = data[start:end]
            
            packet = pack_packet(frame_id, chunk_id, total_chunks, payload)
            sock.sendto(packet, ADDR)

        frame_id += 1
        if cv2.waitKey(1) == 27: break

except KeyboardInterrupt:
    pass

finally:
    cap.release()
    print("Stopped.")