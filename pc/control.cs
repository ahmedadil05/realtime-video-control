using System;
using System.Net.Sockets;
using System.Text;

class ControlClient
{
    static void Main()
    {
        var client = new TcpClient("100.122.162.65", 5000);
        var stream = client.GetStream();

        while (true)
        {
            Console.Write("> ");
            string cmd = Console.ReadLine();
            byte[] data = Encoding.UTF8.GetBytes(cmd + "\n");
            stream.Write(data, 0, data.Length);

            byte[] buffer = new byte[1024];
            int len = stream.Read(buffer, 0, buffer.Length);
            Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, len));
        }
    }
}
