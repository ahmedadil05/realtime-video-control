using System;
using System.Net.Sockets;
using System.Text;

class ControlClient
{
    static void Main()
    {
        try
        {
            // FIX: Changed port from 9000 to 5000 to match the Pi server
            // REPLACE "100.x.x.x" with your Raspberry Pi's actual Tailscale IP
            var client = new TcpClient("100.122.162.65", 5000);
            var stream = client.GetStream();

            Console.WriteLine("Connected to Control Server!");
            Console.WriteLine("Commands: START, STOP, PAUSE, RESUME, RESET");

            while (true)
            {
                Console.Write("> ");
                string cmd = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                // Send command
                byte[] data = Encoding.UTF8.GetBytes(cmd + "\n");
                stream.Write(data, 0, data.Length);

                // Receive response
                byte[] buffer = new byte[1024];
                int len = stream.Read(buffer, 0, buffer.Length);
                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, len));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Make sure the Pi is running 'control_tcp.py' and the IP is correct.");
            Console.ReadKey();
        }
    }
}