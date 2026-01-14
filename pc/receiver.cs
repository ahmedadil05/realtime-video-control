Here is the full, corrected code for `pc/receiver.cs`.

This version implements **Fix 2** by updating the `UnpackPacket` method to match the Python sender's `!IHH` protocol (Big Endian: 4-byte Frame ID, 2-byte Chunk ID, 2-byte Total Chunks). I have added logic to handle the Endianness conversion (Big Endian to Little Endian) so that the integer values are read correctly on your PC.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenCvSharp;

namespace UdpVideoReceiver
{
    public class FrameData
    {
        public Dictionary<int, byte[]> Chunks { get; set; }
        public int Total { get; set; }
        public DateTime Time { get; set; }

        public FrameData(int total)
        {
            Chunks = new Dictionary<int, byte[]>();
            Total = total;
            Time = DateTime.Now;
        }
    }

    public class ReceiverStats
    {
        public int Received { get; set; }
        public int Displayed { get; set; }
        public int Dropped { get; set; }
        public int Incomplete { get; set; }

        public void Reset()
        {
            Received = 0;
            Displayed = 0;
            Dropped = 0;
            Incomplete = 0;
        }
    }

    public class VideoReceiver
    {
        private const int PORT = 5000;
        private const double MAX_FRAME_AGE = 0.5; // seconds
        private const int RECV_BUFFER_SIZE = 65536 * 10; // Increased buffer
        private const int DISPLAY_BUFFER_SIZE = 3;

        private UdpClient udpClient;
        private Dictionary<int, FrameData> frames;
        private Queue<Mat> displayBuffer;
        private ReceiverStats stats;
        private DateTime lastStatsTime;
        private bool isRunning;

        public VideoReceiver()
        {
            frames = new Dictionary<int, FrameData>();
            displayBuffer = new Queue<Mat>();
            stats = new ReceiverStats();
            lastStatsTime = DateTime.Now;
        }

        public void Start()
        {
            try
            {
                // Bind to all interfaces on PORT
                udpClient = new UdpClient(PORT);
                udpClient.Client.ReceiveTimeout = 10; // 10ms timeout
                
                // Increase OS-level receive buffer to reduce packet loss
                try
                {
                    udpClient.Client.ReceiveBufferSize = RECV_BUFFER_SIZE;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not set socket buffer size: {ex.Message}");
                }

                Console.WriteLine($"Receiver listening on port {PORT}...");
                Console.WriteLine("Press ESC to quit\n");

                isRunning = true;
                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting receiver: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private void Run()
        {
            DateTime lastCleanup = DateTime.Now;
            
            using (var window = new Window("Video", WindowFlags.AutoSize))
            {
                while (isRunning)
                {
                    DateTime now = DateTime.Now;

                    // Receive and process packets (burst process to drain buffer)
                    int packetsProcessed = 0;
                    while (packetsProcessed < 50) // Increased batch size
                    {
                        try
                        {
                            IPEndPoint remoteEP = null;
                            byte[] data = udpClient.Receive(ref remoteEP);
                            packetsProcessed++;

                            ProcessPacket(data, now);
                        }
                        catch (SocketException)
                        {
                            // Timeout or no data available
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Socket error: {ex.Message}");
                            break;
                        }
                    }

                    // Display frame if available
                    DisplayFrame(window);

                    // Periodic cleanup (every 100ms)
                    if ((now - lastCleanup).TotalSeconds > 0.1)
                    {
                        CleanupOldFrames(now);
                        lastCleanup = now;
                    }

                    // Print statistics
                    PrintStats();

                    // Handle keyboard input
                    int key = Cv2.WaitKey(1);
                    if (key == 27) // ESC key
                    {
                        Console.WriteLine("\nShutting down...");
                        isRunning = false;
                    }
                }
            }
        }

        private void ProcessPacket(byte[] data, DateTime now)
        {
            try
            {
                // Unpack packet using the corrected protocol
                var (frameId, chunkId, totalChunks, payload) = UnpackPacket(data);

                // Initialize frame if new
                if (!frames.ContainsKey(frameId))
                {
                    frames[frameId] = new FrameData(totalChunks);
                }

                FrameData frame = frames[frameId];
                frame.Chunks[chunkId] = payload;

                // Check if frame is complete
                if (frame.Chunks.Count == frame.Total)
                {
                    // Reassemble frame
                    byte[] ordered = ReassembleFrame(frame);

                    // Process and buffer frame
                    ProcessFrame(ordered);
                    frames.Remove(frameId);
                }
            }
            catch (Exception ex)
            {
                // Often caused by corrupt packets or partial reads
                // Console.WriteLine($"Packet processing error: {ex.Message}");
            }
        }

        private (int frameId, int chunkId, int totalChunks, byte[] payload) UnpackPacket(byte[] data)
        {
            // Python sends struct "!IHH" (Network Byte Order / Big Endian)
            // I (4 bytes) = Frame ID
            // H (2 bytes) = Chunk ID
            // H (2 bytes) = Total Chunks
            // Total Header Size = 8 bytes

            if (data.Length < 8)
            {
                throw new Exception("Packet too small");
            }

            // Read Frame ID (4 bytes)
            byte[] frameIdBytes = new byte[4];
            Array.Copy(data, 0, frameIdBytes, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(frameIdBytes);
            int frameId = BitConverter.ToInt32(frameIdBytes, 0);

            // Read Chunk ID (2 bytes)
            byte[] chunkIdBytes = new byte[2];
            Array.Copy(data, 4, chunkIdBytes, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(chunkIdBytes);
            int chunkId = BitConverter.ToUInt16(chunkIdBytes, 0);

            // Read Total Chunks (2 bytes)
            byte[] totalChunksBytes = new byte[2];
            Array.Copy(data, 6, totalChunksBytes, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(totalChunksBytes);
            int totalChunks = BitConverter.ToUInt16(totalChunksBytes, 0);

            // Extract Payload
            byte[] payload = new byte[data.Length - 8];
            Array.Copy(data, 8, payload, 0, payload.Length);

            return (frameId, chunkId, totalChunks, payload);
        }

        private byte[] ReassembleFrame(FrameData frame)
        {
            int totalSize = frame.Chunks.Values.Sum(chunk => chunk.Length);
            byte[] result = new byte[totalSize];
            int offset = 0;

            for (int i = 0; i < frame.Total; i++)
            {
                if (frame.Chunks.TryGetValue(i, out byte[] chunk))
                {
                    Array.Copy(chunk, 0, result, offset, chunk.Length);
                    offset += chunk.Length;
                }
                else
                {
                    // Should not happen if count == total, but good for safety
                    throw new Exception("Missing chunk during reassembly");
                }
            }

            return result;
        }

        private void ProcessFrame(byte[] frameData)
        {
            try
            {
                // Decode JPEG
                Mat img = Cv2.ImDecode(frameData, ImreadModes.Color);
                
                if (img != null && !img.Empty())
                {
                    // Add to display buffer (maintain max size)
                    if (displayBuffer.Count >= DISPLAY_BUFFER_SIZE)
                    {
                        Mat oldFrame = displayBuffer.Dequeue();
                        oldFrame?.Dispose();
                        stats.Dropped++;
                    }
                    
                    displayBuffer.Enqueue(img);
                    stats.Received++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decoding frame: {ex.Message}");
            }
        }

        private void DisplayFrame(Window window)
        {
            if (displayBuffer.Count > 0)
            {
                Mat img = displayBuffer.Dequeue();
                window.ShowImage(img);
                img.Dispose();
                stats.Displayed++;
            }
        }

        private void CleanupOldFrames(DateTime now)
        {
            List<int> expired = new List<int>();

            foreach (var kvp in frames)
            {
                if ((now - kvp.Value.Time).TotalSeconds > MAX_FRAME_AGE)
                {
                    expired.Add(kvp.Key);
                    stats.Incomplete++;
                }
            }

            foreach (int frameId in expired)
            {
                frames.Remove(frameId);
            }
        }

        private void PrintStats()
        {
            DateTime now = DateTime.Now;
            double elapsed = (now - lastStatsTime).TotalSeconds;

            if (elapsed >= 5.0) // Print every 5 seconds
            {
                double fps = stats.Displayed / elapsed;
                Console.WriteLine($"\n--- Stats (last {elapsed:F1}s) ---");
                Console.WriteLine($"Received: {stats.Received} | Displayed: {stats.Displayed} ({fps:F1} FPS)");
                Console.WriteLine($"Dropped: {stats.Dropped} | Incomplete: {stats.Incomplete}");
                Console.WriteLine($"Buffered frames: {frames.Count}");

                stats.Reset();
                lastStatsTime = now;
            }
        }

        private void Cleanup()
        {
            isRunning = false;
            
            // Dispose all buffered frames
            while (displayBuffer.Count > 0)
            {
                Mat img = displayBuffer.Dequeue();
                img?.Dispose();
            }

            udpClient?.Close();
            Cv2.DestroyAllWindows();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            VideoReceiver receiver = new VideoReceiver();
            
            try
            {
                receiver.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}

```