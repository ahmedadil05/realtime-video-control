import socket
import time
from protocol import pack_header

ADDR = ("100.122.162.65", 5000)
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

seq = 0

while True:
    payload = b"x" * 1024  # fake frame chunk
    header = pack_header(seq, time.time(), len(payload))
    sock.sendto(header + payload, ADDR)

    seq += 1
    time.sleep(0.03)  # ~33 FPS
