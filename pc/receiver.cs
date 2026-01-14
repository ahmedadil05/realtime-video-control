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
        private const string HOST = "100.122.162.65";
        private const int PORT = 5000;
        private const double MAX_FRAME_AGE = 0.5; // seconds
        private const int RECV_BUFFER_SIZE = 65536;
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
                udpClient = new UdpClient(PORT);
                udpClient.Client.ReceiveTimeout = 10; // 10ms timeout
                
                // Increase OS-level receive buffer
                try
                {
                    udpClient.Client.ReceiveBufferSize = RECV_BUFFER_SIZE;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not set socket buffer size: {ex.Message}");
                }

                Console.WriteLine($"Receiver listening on {HOST}:{PORT}...");
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

                    // Receive and process packets (up to 10 per iteration)
                    int packetsProcessed = 0;
                    while (packetsProcessed < 10)
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
                // Unpack packet (you'll need to implement this based on your protocol)
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
                Console.WriteLine($"Packet processing error: {ex.Message}");
            }
        }

        private (int frameId, int chunkId, int totalChunks, byte[] payload) UnpackPacket(byte[] data)
        {
            // Implement based on your protocol format
            // Example implementation (adjust based on your actual protocol):
            int frameId = BitConverter.ToInt32(data, 0);
            int chunkId = BitConverter.ToInt32(data, 4);
            int totalChunks = BitConverter.ToInt32(data, 8);
            byte[] payload = new byte[data.Length - 12];
            Array.Copy(data, 12, payload, 0, payload.Length);

            return (frameId, chunkId, totalChunks, payload);
        }

        private byte[] ReassembleFrame(FrameData frame)
        {
            int totalSize = frame.Chunks.Values.Sum(chunk => chunk.Length);
            byte[] result = new byte[totalSize];
            int offset = 0;

            for (int i = 0; i < frame.Total; i++)
            {
                byte[] chunk = frame.Chunks[i];
                Array.Copy(chunk, 0, result, offset, chunk.Length);
                offset += chunk.Length;
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