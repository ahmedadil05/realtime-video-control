import socket

HOST = "0.0.0.0"
PORT = 9000

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.bind((HOST, PORT))
sock.listen(1)

print("Control server listening...")

conn, addr = sock.accept()
print("Connected from", addr)

while True:
    data = conn.recv(1024)
    if not data:
        break

    command = data.decode().strip()
    print("Command:", command)

    if command == "START":
        conn.sendall(b"OK STARTING\n")
    elif command == "STOP":
        conn.sendall(b"OK STOPPING\n")
    else:
        conn.sendall(b"UNKNOWN COMMAND\n")
