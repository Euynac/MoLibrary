using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.Tool.Web
{
    /// <summary>
    /// DownloadTool on web.
    /// </summary>
    public class DownloadTool
    {
        /// <summary>
        /// Download file from web using given uri to the specific path.
        /// </summary>
        /// <param name="path">Path with file name (file name must with extension but will change it into resource type at the end)</param>
        /// <param name="uri"></param>
        /// <returns>Get the downloaded file path, and if failed, return null</returns>
        public static string DownloadFile(string path, Uri uri)
        {
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            var task = Task.Factory.StartNew(() => _downloadFile(directory, fileName, uri));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Download file from web using given uri to the specific path.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="fileName">file name without extension. (extension defined by the given resource in uri)</param>
        /// <param name="uri"></param>
        /// <returns>Get the downloaded file path, and if failed, return null</returns>
        public static string DownloadFile(string directoryPath, string fileName, Uri uri)
        {
            var task = Task.Factory.StartNew(() => _downloadFile(directoryPath, fileName, uri));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Download file async from web using given uri to the specific path.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="fileName">file name without extension. (extension defined by the given resource in uri)</param>
        /// <param name="uri"></param>
        /// <returns>Get the downloaded file path, and if failed, return null</returns>
        public static async Task<string> DownloadFileAsync(string directoryPath, string fileName, Uri uri)
        {
            return await Task.Factory.StartNew(() => _downloadFile(directoryPath, fileName, uri));
        }

        private static readonly HttpClient _downloader = new();
        /// <summary>
        /// Get file size use HEAD request to get Content-Length from given url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static long GetFileSize(string url)
        {
            long result = 0;

            var req = WebRequest.Create(url);
            req.Method = "HEAD";
            using var resp = req.GetResponse();
            if (long.TryParse(resp.Headers.Get("Content-Length"), out var contentLength))
            {
                result = contentLength;
            }

            return result;
        }
        private static string _downloadFile(string directoryPath, string fileName, Uri uri)
        {
            // Get the file extension
            var uriWithoutQuery = uri.GetLeftPart(UriPartial.Path); //remove redundant query arguments in uri.
            var fileExtension = Path.GetExtension(uriWithoutQuery);
            if (fileName.IsNullOrEmpty()) fileName = FileTool.GetTimestampRandomFileName();
            // Create file path and ensure directory exists
            var path = Path.Combine(directoryPath, $"{fileName}{fileExtension}");
            Directory.CreateDirectory(directoryPath);

            // Download the file and write to the file
            var task = _downloader.GetByteArrayAsync(uri);
            task.Wait();
            var fileBytes = task.Result;
            File.WriteAllBytes(path, fileBytes);
            return path;
        }
    }
}