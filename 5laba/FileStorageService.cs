using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace FileStorageConsole
{
    public class FileStorageService
    {
        private readonly string _storagePath;

        public FileStorageService(string storagePath)
        {
            _storagePath = Path.GetFullPath(storagePath);
            Directory.CreateDirectory(_storagePath);
            Console.WriteLine($"Хранилище инициализировано: {_storagePath}");
        }

        private string GetSafeFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return _storagePath;

            string normalized = relativePath.TrimEnd('/', '\\');
            string combined = Path.Combine(_storagePath, normalized);
            string fullPath = Path.GetFullPath(combined);

            if (!fullPath.StartsWith(_storagePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.Equals(_storagePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Попытка path traversal");
            }

            return fullPath;
        }

        public bool ExistsAsFile(string relativePath)
        {
            string full = GetSafeFullPath(relativePath);
            return File.Exists(full);
        }

        public bool ExistsAsDirectory(string relativePath)
        {
            string full = GetSafeFullPath(relativePath);
            return Directory.Exists(full);
        }

        public List<string> GetListOfFiles(string relativeDir = "")
        {
            string fullDir = GetSafeFullPath(relativeDir);
            if (!Directory.Exists(fullDir))
                throw new DirectoryNotFoundException("Каталог не найден");

            return Directory.EnumerateFiles(fullDir, "*.*", SearchOption.AllDirectories)
                .Select(f => GetRelativePath(f))
                .OrderBy(p => p)
                .ToList();
        }

        private string GetRelativePath(string fullFilePath)
        {
            string rel = fullFilePath.Substring(_storagePath.Length).TrimStart(Path.DirectorySeparatorChar);
            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }

        public void ServeFile(string relativePath, HttpListenerResponse response)
        {
            string full = GetSafeFullPath(relativePath);
            if (!File.Exists(full))
                throw new FileNotFoundException();

            string mimeType = GetMimeType(full);
            response.ContentType = mimeType;

            using var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            response.ContentLength64 = fs.Length;
            fs.CopyTo(response.OutputStream);
        }

        public void SendFileInfo(string relativePath, HttpListenerResponse response)
        {
            string full = GetSafeFullPath(relativePath);
            if (!File.Exists(full))
                throw new FileNotFoundException();

            var fi = new FileInfo(full);
            response.Headers.Add("Filename", fi.Name);
            response.Headers.Add("Length", fi.Length.ToString());
            response.Headers.Add("Last-modified", fi.LastWriteTime.ToString());
        }

        public void UploadFile(string relativePath, Stream inputStream)
        {
            string full = GetSafeFullPath(relativePath);
            string? dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var fs = new FileStream(full, FileMode.Create);
            inputStream.CopyTo(fs);
        }

        public void CopyFile(string sourceRelative, string targetRelative)
        {
            string sourceFull = GetSafeFullPath(sourceRelative);
            if (!File.Exists(sourceFull))
                throw new FileNotFoundException();

            string targetFull = GetSafeFullPath(targetRelative);
            string? dir = Path.GetDirectoryName(targetFull);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(sourceFull, targetFull, overwrite: true);
        }

        public void Delete(string relativePath)
        {
            string full = GetSafeFullPath(relativePath);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
            else if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        private static string GetMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".txt" => "text/plain; charset=utf-8",
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }
    }
}