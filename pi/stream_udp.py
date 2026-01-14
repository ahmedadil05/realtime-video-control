import socket
import cv2
import math
from protocol import pack_packet # Changed from pack_header

ADDR = ("100.93.237.108", 5000)
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
MAX_PAYLOAD_SIZE = 1024

cap = cv2.VideoCapture(0)
frame_id = 0

while True:
    ret, frame = cap.read()
    if not ret: break

    # 1. Resize and Encode
    frame = cv2.resize(frame, (640, 480))
    _, encoded = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 50])
    data = encoded.tobytes()

    # 2. Calculate Chunks
    file_size = len(data)
    total_chunks = math.ceil(file_size / MAX_PAYLOAD_SIZE)

    # 3. Send Chunks
    for chunk_id in range(total_chunks):
        start = chunk_id * MAX_PAYLOAD_SIZE
        end = min(start + MAX_PAYLOAD_SIZE, file_size)
        payload = data[start:end]
        
        # Uses the correct protocol function
        packet = pack_packet(frame_id, chunk_id, total_chunks, payload)
        sock.sendto(packet, ADDR)

    frame_id += 1
    if cv2.waitKey(1) == 27: break

cap.release()