
# Real-Time Video Streaming & Control over Tailscale



This project demonstrates a low-latency, real-time video streaming system using a Raspberry Pi camera and a Windows PC receiver. The architecture is designed for simplicity and performance, making it a great starting point for robotics, drones, or telepresence projects.



The system separates fast, real-time video data from reliable control commands:

- **UDP → Video Data**: For high-speed, low-latency frame delivery. Dropped packets are acceptable.

- **TCP → Control Commands**: For guaranteed, in-order delivery of commands like `START` and `STOP`.

- **Tailscale → Secure Networking**: Creates a private, secure network, allowing the devices to communicate as if they were on the same LAN without any port forwarding.



## High-Level Architecture



```

┌───────────────────┐      Tailscale      ┌───────────────────┐

│   Raspberry Pi    │◄───────────────────►│    Windows PC     │

│ (Sender)          │     100.x.x.x       │    (Receiver)     │

│                   │                     │                   │

│  ┌─────────────┐  │                     │  ┌─────────────┐  │

│  │  main.py    │  │                     │  │ control.cs  │──┼─► Send Commands

│  │ (Threaded)  │  │                     │  │ (TCP Client)│  │

│  └──────┬──────┘  │                     │  └─────────────┘  │

│         │         │                     │         ▲         │

│ ╔═══════▼═══════╗ │         TCP         │         │         │

│ ║ Control Port  ║◄┼─────────────────────┼─────────┘         │

│ ║ (5000)        ║ │      Commands       │                   │

│ ╚═══════▲═══════╝ │                     │                   │

│         │ Control │                     │  ╔═════════════╗  │

│ ╔═══════▼═══════╗ │         UDP         │  ║ Video Port    ║  │

│ ║ Video Port    ║─┼────────────────────►┼─►║ (5001)        ║  │

│ ║ (5001)        ║ │     Video Stream    │  ╚═════════════╝  │

│ ╚═══════════════╝ │                     │         │         │

│                   │                     │  ┌──────▼──────┐  │

│                   │                     │  │ receiver.cs │  │

│                   │                     │  │ (UDP Server)│  │

│                   │                     │  └─────────────┘  │

└───────────────────┘                     └───────────────────┘

```



## Repository Structure



```

realtime-video-control/

├── pi/

│   └── main.py           # Single, threaded script for camera capture, streaming, and control.

│

├── pc/

│   ├── receiver.cs       # C# UDP video receiver, display, and recording.

│   ├── control.cs        # C# TCP client for sending commands.

│   └── ui/               # (Placeholder for UI elements if developed further)

│

├── docs/

│   └── architecture.md   # Original design notes & diagrams.

│

└── README.md

```



## Protocols



#### Video Data Protocol (UDP Port 5001)

Each UDP packet contains a single, complete frame.



`[SEQ][TIMESTAMP][JPEG_FRAME_BYTES]`



- **SEQ** (uint32): A sequence number for each frame.

- **TIMESTAMP** (uint64): A high-precision timestamp (in nanoseconds) from the sender, used to calculate latency.

- **JPEG data**: The raw bytes of the JPEG-compressed video frame.



#### Control Protocol (TCP Port 5000)

Control commands are simple ASCII messages, terminated by a newline.

- `START`: Tells the Raspberry Pi to begin streaming video.

- `STOP`: Tells the Raspberry Pi to pause the video stream.



---



## How to Run



### 1. Prerequisites

- **Install Tailscale** on both the Raspberry Pi and the Windows PC. Log in with the same account on both and ensure they can ping each other using their `100.x.x.x` IP addresses.

- **Git:** You need Git installed on the Pi to clone the repository.



### 2. Raspberry Pi (Sender) Setup



1.  **Clone the Repository:**

    ```bash

    git clone https://github.com/your-username/realtime-video-control.git

    cd realtime-video-control/pi

    ```



2.  **Install Dependencies:**

    This only needs to be done once. This command installs OpenCV, which is required to capture video from the camera.

    ```bash

    sudo apt update

    sudo apt install python3-opencv

    ```



3.  **Configure the PC's IP Address:**

    Open the `main.py` script in a text editor like `nano`.

    ```bash

    nano main.py

    ```

    Find the `PC_IP` variable and change it to your **Windows PC's** Tailscale IP address.

    ```python

    PC_IP = "100.x.x.x"  # CHANGE THIS to your PC's Tailscale IP

    ```

    Press `Ctrl+X`, then `Y`, then `Enter` to save and exit.



4.  **Run the Application:**

    ```bash

    python3 main.py

    ```

    The terminal will show that the server is listening and waiting for a `START` command.



### 3. Windows PC (Receiver) Setup



You need to run two separate applications: the **Control Client** and the **Video Receiver**.



1.  **Run the Video Receiver:**

    - Build and run the `pc/receiver.cs` project.

    - A window titled "Real-time Video" will open, waiting for the stream.

    - Console messages will indicate that it is listening on port 5001.



2.  **Run the Control Client:**

    - Open `pc/control.cs` and ensure the IP address matches your **Raspberry Pi's** Tailscale IP.

    - Build and run the `control.cs` project.

    - A console window will appear. Type `START` and press Enter to begin the video stream.

    - The video should now appear in the "Real-time Video" window.

    - You can type `STOP` to pause the stream at any time.



---



## Features



### Real-time Latency Display

The video window displays the end-to-end latency in milliseconds, calculated from the timestamp sent by the Raspberry Pi. This provides immediate feedback on network performance.



### Video and Metadata Recording



The receiver application can record the video stream and its associated metadata.



- **How to Use:** Press the **`r` key** in the video window to toggle recording on and off.

- **Indicator:** A red circle and "REC" text will appear in the top-left corner of the window while recording is active.

- **Output Files:** When you stop recording, two files are saved with a timestamped name:

    - `video_YYYYMMDD_HHMMSS.mp4`: A standard H.264 video file containing the recorded frames.

    - `meta_YYYYMMDD_HHMMSS.csv`: A CSV log containing the `Timestamp`, `SequenceNumber`, and `LatencyMs` for every recorded frame. This data is perfect for analysis in Excel or other tools.



## License



MIT — build, break, learn, repeat.
