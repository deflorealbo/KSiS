using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileStorageConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string storagePath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "Storage");

            var service = new FileStorageService(storagePath);

            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.2:5001/");
            listener.Start();

            Console.WriteLine("Файловое хранилище запущено");
            Console.WriteLine("Адрес: http://127.0.0.2:5001 /");
            Console.WriteLine();

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequest(context, service)); 
            }
        }

        private static void ProcessRequest(HttpListenerContext context, FileStorageService service)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string method = request.HttpMethod.ToUpperInvariant();
                string path = WebUtility.UrlDecode(request.Url.AbsolutePath).TrimStart('/');

                
                var headers = request.Headers.AllKeys
                    .ToDictionary(k => k!, k => request.Headers[k]!);

                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string body = reader.ReadToEnd();

                

                RouteRequest(method, path, headers, body, service, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }
        private static void RouteRequest(
    string method,
    string path,
    Dictionary<string, string> headers,
    string body,
    FileStorageService service,
    HttpListenerResponse response)
        {
            try
            {
                switch (method)
                {
                    case "GET":
                        HandleGet(path, service, response);
                        break;

                    case "PUT":
                        HandlePut(path, headers, body, service, response);
                        break;

                    case "DELETE":
                        HandleDelete(path, service, response);
                        break;

                    case "HEAD":
                        HandleHead(path, service, response);
                        break;

                    default:
                        response.StatusCode = 405;
                        break;
                }
            }
            catch (FileNotFoundException)
            {
                response.StatusCode = 404;
            }
            catch (UnauthorizedAccessException)
            {
                response.StatusCode = 403;
            }
        }
        private static void HandleGet(string path, FileStorageService service, HttpListenerResponse response)
        {
            if (service.ExistsAsFile(path))
            {
                service.ServeFile(path, response);
                response.StatusCode = 200;
            }
            else if (service.ExistsAsDirectory(path) || string.IsNullOrEmpty(path))
            {
                var files = service.GetListOfFiles(path);
                string json = JsonSerializer.Serialize(files);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        private static void HandlePut(string path, Dictionary<string, string> headers, string body,
    FileStorageService service, HttpListenerResponse response)
        {
            if (headers.TryGetValue("X-Copy-From", out var copyFrom))
            {
                copyFrom = copyFrom.TrimStart('/');
                service.CopyFile(copyFrom, path);
                response.StatusCode = 201;
            }
            else
            {
                if (string.IsNullOrEmpty(body))
                {
                    response.StatusCode = 400;
                }
                else
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
                    service.UploadFile(path, stream);
                    response.StatusCode = 201;
                }
            }
        }
        private static void HandleDelete(string path, FileStorageService service, HttpListenerResponse response)
        {
            service.Delete(path);
            response.StatusCode = 204;
        }
        private static void HandleHead(string path, FileStorageService service, HttpListenerResponse response)
        {
            if (service.ExistsAsFile(path))
            {
                service.SendFileInfo(path, response);
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = 404;
            }
        }
    }
}