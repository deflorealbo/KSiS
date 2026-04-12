using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace simpleProxy
{
    public class ClientHandler
    {
        private TcpClient client;

        public ClientHandler(TcpClient client)
        {
            this.client = client;
        }

        public void Handle()
        {
            using var clientStream = client.GetStream();
            using var reader = new StreamReader(clientStream);
            using var writer = new StreamWriter(clientStream) { AutoFlush = true };

            try
            {
                string requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine) || requestLine.StartsWith("CONNECT"))
                    return;

                var requestLines = new List<string> { requestLine };

                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    requestLines.Add(line);
                }

                string host = GetHost(requestLines);
                int port = GetPort(requestLines);

                using (var blacklist = new BlackListHandler())
                {
                    if (blacklist.IsBlocked(host))
                    {
                        blacklist.WriteForbiddenMessage(clientStream);
                        return;
                    }
                }

                Console.WriteLine($"Request: {requestLine}");

                using var server = new TcpClient(host, port);
                using var serverStream = server.GetStream();

                string formattedRequest = FormatRequest(requestLines);
                byte[] requestData = Encoding.UTF8.GetBytes(formattedRequest);

                serverStream.Write(requestData, 0, requestData.Length);

                byte[] buffer = new byte[8192];
                int bytesRead;
                bool firstLine = true;

                while ((bytesRead = serverStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    clientStream.Write(buffer, 0, bytesRead);

                    if (firstLine)
                    {
                        string responseLine = Encoding.UTF8.GetString(buffer);
                        Console.WriteLine("Response: " + responseLine.Split("\r\n")[0]);
                        firstLine = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                client.Close();
            }
        }

        private string GetHost(List<string> request)
        {
            foreach (var line in request)
            {
                if (line.ToLower().StartsWith("host:"))
                {
                    var host = line.Substring(5).Trim();
                    return host.Contains(":") ? host.Split(':')[0] : host;
                }
            }
            return null;
        }

        private int GetPort(List<string> request)
        {
            foreach (var line in request)
            {
                if (line.ToLower().StartsWith("host:"))
                {
                    var host = line.Substring(5).Trim();
                    if (host.Contains(":"))
                        return int.Parse(host.Split(':')[1]);
                }
            }
            return 80;
        }

        private string FormatRequest(List<string> request)
        {
            var firstLineParts = request[0].Split(' ');

            try
            {
                var uri = new Uri(firstLineParts[1]);
                request[0] = $"{firstLineParts[0]} {uri.PathAndQuery} {firstLineParts[2]}";
            }
            catch { }

            var sb = new StringBuilder();
            foreach (var line in request)
            {
                sb.Append(line + "\r\n");
            }
            sb.Append("\r\n");

            return sb.ToString();
        }
    }
}
