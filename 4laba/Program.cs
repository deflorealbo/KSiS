using System.Net.Sockets;
using System.Net;

namespace simpleProxy
{
    class Program
    {
        private const int PORT = 5000;

        static void Main()
        {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.2"), PORT);
            listener.Start();

            Console.WriteLine($"Proxy server started on port {PORT}");

            while (true)
            {
                var client = listener.AcceptTcpClient();

                new Thread(() =>
                {
                    var handler = new ClientHandler(client);
                    handler.Handle();
                }).Start();
            }
        }
    }
}