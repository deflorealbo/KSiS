using System.Net.Sockets;
using System.Net;
using System.Text;

internal class Program
{
    static List<Socket> clients = new List<Socket>();
    static object lockObj = new object();

    static void Main(string[] args)
    {
        string ipAddress;
        int port;

        
        while (true)
        {
            try
            {
                Console.Write("Введите IP: ");
                ipAddress = Console.ReadLine();

                Console.Write("Введите порт: ");
                port = int.Parse(Console.ReadLine());

               
                Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                testSocket.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));
                testSocket.Close();

                break; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Порт недоступен: {ex.Message}");
                Console.WriteLine("Попробуйте снова.\n");
            }
        }

        Thread listenerThread = new Thread(() => StartListening(ipAddress, port));
        listenerThread.Start();

        Console.WriteLine("Сервер запущен...");
    }

    static void StartListening(string ip, int port)
    {
        try
        {
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(ipPoint);
            socket.Listen(10);

            Console.WriteLine($"Слушаю {ip}:{port}");

            while (true)
            {
                Socket client = socket.Accept();

                lock (lockObj)
                    clients.Add(client);

                Console.WriteLine($"Подключён: {client.RemoteEndPoint}");

                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка на порту {port}: {ex.Message}");
        }
    }

    static void HandleClient(Socket client)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytes = client.Receive(buffer);

                if (bytes == 0)
                    break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytes);
                string fullMessage = $"{client.RemoteEndPoint}: {message}";

                Console.WriteLine(fullMessage);

                Broadcast(fullMessage, client);
            }
        }
        catch { }

        Console.WriteLine($"Отключён: {client.RemoteEndPoint}");

        lock (lockObj)
            clients.Remove(client);

        client.Close();
    }

    static void Broadcast(string message, Socket sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);

        lock (lockObj)
        {
            foreach (var client in clients)
            {
                try
                {
                    if (client != sender)
                        client.Send(data);
                }
                catch { }
            }
        }
    }
}