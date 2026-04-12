using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace simpleProxy
{
    public class BlackListHandler : IDisposable
    {
        private List<string> blacklist = new List<string>();

        public BlackListHandler()
        {
            if (File.Exists("blacklist.txt"))
            {
                blacklist = new List<string>(File.ReadAllLines("blacklist.txt"));
            }
        }

        public bool IsBlocked(string host)
        {
            foreach (var line in blacklist)
            {
                if (host.Contains(line.Trim()))
                    return true;
            }
            return false;
        }

        public void WriteForbiddenMessage(NetworkStream stream)
        {
            string body = File.Exists("forbiddenPage.html")
                ? File.ReadAllText("forbiddenPage.html")
                : "<h1>403 Forbidden</h1>";

            string response =
                "HTTP/1.1 403 Forbidden\r\n" +
                "Content-Type: text/html\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                "\r\n" +
                body;

            Console.WriteLine("Proxy: HTTP/1.1 403 Forbidden");
            byte[] data = Encoding.UTF8.GetBytes(response);
            stream.Write(data, 0, data.Length);
        }

        public void Dispose() { }
    }
}
