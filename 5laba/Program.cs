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
            Console.WriteLine("Для остановки нажмите Ctrl+C");
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
                string path = request.Url.AbsolutePath.TrimStart('/');

                if (method == "GET")
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

                        response.ContentType = "application/json; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.StatusCode = 200;
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                }
                else if (method == "HEAD")
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
                else if (method == "PUT")
                {
                    string? copyFrom = request.Headers["X-Copy-From"];
                    if (!string.IsNullOrEmpty(copyFrom))
                    {
                        
                        copyFrom = copyFrom.TrimStart('/');
                        service.CopyFile(copyFrom, path);
                        response.StatusCode = 200;
                    }
                    else
                    {
                        
                        if (request.ContentLength64 <= 0)
                        {
                            response.StatusCode = 400;
                        }
                        else
                        {
                            service.UploadFile(path, request.InputStream);
                            response.StatusCode = 201; // Created
                        }
                    }
                }
                else if (method == "DELETE")
                {
                    service.Delete(path);
                    response.StatusCode = 204; // No Content
                }
                else
                {
                    response.StatusCode = 405; // Method Not Allowed
                }
            }
            catch (FileNotFoundException) { response.StatusCode = 404; }
            catch (DirectoryNotFoundException) { response.StatusCode = 404; }
            catch (UnauthorizedAccessException) { response.StatusCode = 403; }
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
    }
}