
# Project Title

A brief description of what this project does and who it's for

Real-Time Video Streaming & Control over Tailscale

This repository demonstrates a real-time video streaming system using a Raspberry Pi camera and a Windows PC receiver, designed with low latency as the primary constraint.

The architecture intentionally separates data and control:

UDP / WebRTC → video data (fast, lossy, real-time)

TCP → control commands (reliable, ordered)

Tailscale → private, secure network between devices

This mirrors how real production systems (drones, telepresence, robotics, AR/VR) are built.

High-Level Architecture
┌─────────────────┐     Tailscale     ┌─────────────────┐
│   Raspberry Pi  │◄─────────────────►│   Windows PC    │
│                 │    100.x.x.x      │                 │
│  ┌───────────┐  │                   │  ┌───────────┐  │
│  │  Camera   │  │                   │  │ C# App    │  │
│  │  OV5647   │  │                   │  │ Receiver  │  │
│  └─────┬─────┘  │                   │  └─────┬─────┘  │
│        │        │                   │        │        │
│  ┌─────▼─────┐  │    UDP / WebRTC   │  ┌─────▼─────┐  │
│  │ Streaming │──┼───────────────────┼─►│ Display   │  │
│  │   Server  │  │   Real-time       │  │ + Metrics │  │
│  └─────┬─────┘  │                   │  └─────┬─────┘  │
│        │        │                   │        │        │
│  ┌─────▼─────┐  │      TCP          │        │        │
│  │ Control   │◄─┼───────────────────┼────────┘        │
│  │ Receiver  │  │   Commands        │                 │
│  └───────────┘  │                   │                 │
└─────────────────┘                   └─────────────────┘

Why This Design
Why UDP for video?

No retransmissions → lower latency

Packet loss is acceptable for video

This is how RTP, WebRTC, and live streaming work

Why TCP for control?

Commands must not be lost

Order matters (PAUSE before STOP)

Reliability beats speed for control signals

Why Tailscale?

No port forwarding

Encrypted mesh VPN

Devices talk as if on the same LAN

Each device gets a stable 100.x.x.x IP

Repository Structure
realtime-video-control/
├── pi/
│   ├── camera.py        # Camera capture (OpenCV)
│   ├── stream_udp.py    # UDP video streaming
│   ├── control_tcp.py   # TCP command listener
│   └── protocol.py     # Shared packet definitions
│
├── pc/
│   ├── receiver.cs     # C# UDP video receiver
│   ├── control.cs      # TCP control client
│   └── ui/             # Display & controls
│
├── docs/
│   └── architecture.md # Design notes & diagrams
│
└── README.md

Video Data Protocol (UDP)

Each UDP packet contains:

[SEQ][TIMESTAMP][JPEG_FRAME_BYTES]


SEQ (uint32): detects packet loss

TIMESTAMP (uint64): latency measurement

JPEG data: compressed frame

UDP does not have a concept of “ending” a stream.
The sender simply stops sending packets.

Control Protocol (TCP)

Control commands are simple ASCII messages:

START
STOP
PAUSE
RESUME
RESET


TCP ensures:

Commands arrive

Commands arrive once

Commands arrive in order

How to Run
1. Install Tailscale (both devices)

Log in with the same account

Confirm both devices can ping each other via 100.x.x.x

2. Raspberry Pi (Sender)
cd pi
python3 control_tcp.py
python3 stream_udp.py


The Pi:

Captures frames from the camera

Encodes frames

Sends them over UDP

Listens for TCP control commands

3. Windows PC (Receiver)

Build and run the C# receiver

Enter the Raspberry Pi Tailscale IP

Start receiving video

Send control commands via TCP

Metrics You Can Measure

Packet loss (via sequence numbers)

One-way latency (timestamps)

Jitter (variation in arrival time)

FPS at sender vs receiver

This is the foundation for adaptive bitrate, congestion control, and QoS.

Future Extensions

Replace raw UDP with WebRTC

Hardware encoding (H.264 via Pi GPU)

Forward Error Correction (FEC)

AR / XR integration

Multi-receiver broadcasting

Encryption at application layer

Important Note

This project is not a media player.
It is a networked real-time system.

Dropped packets are normal.
Late packets are useless.
Latency is the enemy.

License

MIT — build, break, learn, repeat.