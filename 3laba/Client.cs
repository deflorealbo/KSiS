using System.Net.Sockets;
using System.Net;
using System.Text;

internal class Program
{
    static Socket socket;

    static void Main(string[] args)
    {
        while (true)
        {
            try
            {
                Console.Write("Введите свой IP: ");
                string localIP = Console.ReadLine();

                Console.Write("Введите свой порт: ");
                int localPort = int.Parse(Console.ReadLine());

                Console.Write("Введите IP сервера: ");
                string serverIP = Console.ReadLine();

                Console.Write("Введите порт сервера: ");
                int serverPort = int.Parse(Console.ReadLine());

                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                socket.Bind(new IPEndPoint(IPAddress.Parse(localIP), localPort));

                socket.Connect(new IPEndPoint(IPAddress.Parse(serverIP), serverPort));

                Console.WriteLine("Подключено!");

                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                Console.WriteLine("Попробуйте снова...\n");
            }
        }

        Thread receiveThread = new Thread(ReceiveMessages);
        receiveThread.Start();

        StringBuilder input = new StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                string message = input.ToString();
                input.Clear();

                byte[] data = Encoding.UTF8.GetBytes(message);
                socket.Send(data);

                Console.WriteLine();
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input.Remove(input.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else
            {
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }

    static void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytes = socket.Receive(buffer);

                if (bytes == 0)
                    break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytes);

                Console.WriteLine($"\n{message}");
            }
        }
        catch
        {
            Console.WriteLine("\nСоединение разорвано");
        }
    }
}