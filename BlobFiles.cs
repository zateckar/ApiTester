using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ApiTester
{
    /// <summary>
    /// The blob container as an <see cref="IFilesBackend"/> - the backend the Files tab always
    /// had. Plain transport: it takes and returns data, never touches a control and never logs,
    /// so a transfer can also run on a worker thread - which is what a drag out to Explorer does.
    /// </summary>
    internal sealed class AzureBlobFilesBackend : IFilesBackend
    {
        //Its own client rather than the sync's: a transfer is bounded by the user cancelling it,
        //not by a clock, and HttpClient's 100 second default covers the whole response including
        //the body - long enough to abort a large file halfway through.
        private static readonly HttpClient fileClient = new() { Timeout = Timeout.InfiniteTimeSpan };

        //Below this a file goes up in one request; above it in blocks, so memory stays flat and
        //progress keeps moving on a file that takes a while.
        private const long FileSingleShotLimit = 8L * 1024 * 1024;

        private const int FileBlockSize = 4 * 1024 * 1024;
        private const int FileCopyBuffer = 256 * 1024;

        // ---------------------------------------------------------------- addressing

        /// <summary>
        /// Escapes a blob name for use in a URL, one segment at a time - the slashes between
        /// them are path separators and have to survive.
        /// </summary>
        private static string EscapeBlobPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            string[] segments = path.Split('/');

            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = Uri.EscapeDataString(segments[i]);
            }

            return string.Join("/", segments);
        }

        private static void EnsureBlobSuccess(HttpResponseMessage response, string what, bool allowMissing = false)
        {
            if (response.IsSuccessStatusCode) return;
            if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) return;

            throw new BlobFileException(what + " failed: " + ReadBlobFailureDetail(response));
        }

        /// <summary>
        /// Synchronous face of the shared detail reader: the transfer pipeline cannot await
        /// inside EnsureBlobSuccess without reshaping every caller.
        /// </summary>
        private static string ReadBlobFailureDetail(HttpResponseMessage response)
            => AzureBlobStore.BlobFailureDetail(response).GetAwaiter().GetResult();

        // ---------------------------------------------------------------- listing

        /// <summary>
        /// Lists one folder: the blobs directly under <paramref name="prefix"/> plus the
        /// prefixes that stand for the folders below it.
        /// </summary>
        /// <param name="prefix">Empty for the container root, otherwise ends in a slash.</param>
        public async Task<List<BlobFile>> Browse(string prefix, CancellationToken ct)
        {
            var entries = new List<BlobFile>();
            string marker = null;

            do
            {
                XmlDocument page = await BlobListPage(prefix, "&delimiter=%2F", marker, ct);

                XmlNodeList folders = page.SelectNodes("/EnumerationResults/Blobs/BlobPrefix");

                if (folders is not null)
                {
                    foreach (XmlNode folder in folders)
                    {
                        string name = folder.SelectSingleNode("Name")?.InnerText;
                        if (string.IsNullOrEmpty(name)) continue;

                        entries.Add(new BlobFile { BlobPath = name, IsFolder = true });
                    }
                }

                XmlNodeList blobs = page.SelectNodes("/EnumerationResults/Blobs/Blob");

                if (blobs is not null)
                {
                    foreach (XmlNode blob in blobs)
                    {
                        string name = blob.SelectSingleNode("Name")?.InnerText;
                        if (string.IsNullOrEmpty(name)) continue;

                        //The empty blob that keeps an otherwise empty folder on the listing is
                        //the folder itself, not something inside it.
                        if (name.EndsWith('/')) continue;

                        entries.Add(ReadBlobProperties(blob, name));
                    }
                }

                marker = page.SelectSingleNode("/EnumerationResults/NextMarker")?.InnerText;
            }
            while (!string.IsNullOrEmpty(marker));

            return entries;
        }

        /// <summary>
        /// Every blob under a prefix, however deep. Folders do not appear - only the files that
        /// make them up, which is what a recursive download, move or delete works on.
        /// </summary>
        public async Task<List<BlobFile>> BrowseTree(string prefix, CancellationToken ct)
        {
            var entries = new List<BlobFile>();
            string marker = null;

            do
            {
                XmlDocument page = await BlobListPage(prefix, string.Empty, marker, ct);

                XmlNodeList blobs = page.SelectNodes("/EnumerationResults/Blobs/Blob");

                if (blobs is not null)
                {
                    foreach (XmlNode blob in blobs)
                    {
                        string name = blob.SelectSingleNode("Name")?.InnerText;
                        if (string.IsNullOrEmpty(name)) continue;
                        if (name.EndsWith('/')) continue;

                        entries.Add(ReadBlobProperties(blob, name));
                    }
                }

                marker = page.SelectSingleNode("/EnumerationResults/NextMarker")?.InnerText;
            }
            while (!string.IsNullOrEmpty(marker));

            return entries;
        }

        private static async Task<XmlDocument> BlobListPage(string prefix, string extraQuery, string marker, CancellationToken ct)
        {
            string query = "&restype=container&comp=list"
                + extraQuery
                + "&prefix=" + Uri.EscapeDataString(prefix ?? string.Empty)
                + (string.IsNullOrEmpty(marker) ? string.Empty : "&marker=" + Uri.EscapeDataString(marker));

            using var request = new HttpRequestMessage(HttpMethod.Get, AzureAuth.ContainerUri(query));
            AzureAuth.AddCommonHeaders(request);
            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            EnsureBlobSuccess(response, "Listing \"" + prefix + "\"");

            var document = new XmlDocument { XmlResolver = null };

            using Stream body = await response.Content.ReadAsStreamAsync(ct);
            using var reader = XmlReader.Create(body, new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit });

            document.Load(reader);

            return document;
        }

        private static BlobFile ReadBlobProperties(XmlNode blob, string name)
        {
            XmlNode properties = blob.SelectSingleNode("Properties");

            long length = 0;
            DateTime? modified = null;

            if (properties is not null)
            {
                long.TryParse(properties.SelectSingleNode("Content-Length")?.InnerText,
                              NumberStyles.Integer, CultureInfo.InvariantCulture, out length);

                if (DateTime.TryParse(properties.SelectSingleNode("Last-Modified")?.InnerText,
                                      CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                      out DateTime parsed))
                {
                    modified = parsed.ToLocalTime();
                }
            }

            return new BlobFile
            {
                BlobPath = name,
                Length = length,
                Modified = modified,
                ContentType = properties?.SelectSingleNode("Content-Type")?.InnerText
            };
        }

        // ---------------------------------------------------------------- transfers

        /// <summary>
        /// Uploads a local file, streaming it rather than reading it into memory first.
        /// </summary>
        /// <param name="progress">Called with each chunk of bytes accepted, never with a total.</param>
        public async Task Upload(string localPath, string remotePath, Action<long> progress, CancellationToken ct)
        {
            var info = new FileInfo(localPath);

            using var source = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileCopyBuffer, useAsync: true);

            if (info.Length <= FileSingleShotLimit)
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(EscapeBlobPath(remotePath)));

                AzureAuth.AddCommonHeaders(request);
                request.Headers.Add("x-ms-blob-type", "BlockBlob");

                request.Content = new StreamContent(source);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(remotePath));
                request.Content.Headers.ContentLength = info.Length;

                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

                EnsureBlobSuccess(response, "Upload of " + remotePath);

                progress?.Invoke(info.Length);
                return;
            }

            //Larger files go up as blocks and are assembled at the end. Nothing is visible in the
            //container until the block list is committed, so an abandoned upload leaves no
            //half-written blob behind - the uncommitted blocks expire on their own.
            var blockIds = new List<string>();
            byte[] buffer = new byte[FileBlockSize];

            while (true)
            {
                int read = await FillBuffer(source, buffer, ct);
                if (read == 0) break;

                //Every id in one blob has to decode to the same length, hence the fixed width.
                string blockId = Convert.ToBase64String(Encoding.ASCII.GetBytes(
                    "blk-" + blockIds.Count.ToString("D8", CultureInfo.InvariantCulture)));

                await BlobPutBlock(remotePath, blockId, buffer, read, ct);

                blockIds.Add(blockId);
                progress?.Invoke(read);
            }

            await BlobPutBlockList(remotePath, blockIds, ct);
        }

        /// <summary>
        /// Reads until the buffer is full or the file ends - a stream is free to hand back less
        /// than was asked for, and a short read would otherwise become a short block.
        /// </summary>
        private static async Task<int> FillBuffer(FileStream source, byte[] buffer, CancellationToken ct)
        {
            int total = 0;

            while (total < buffer.Length)
            {
                int read = await source.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
                if (read == 0) break;

                total += read;
            }

            return total;
        }

        private static async Task BlobPutBlock(string blobPath, string blockId, byte[] buffer, int count, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put,
                AzureAuth.BlobUri(EscapeBlobPath(blobPath), "&comp=block&blockid=" + Uri.EscapeDataString(blockId)));

            AzureAuth.AddCommonHeaders(request);

            request.Content = new ByteArrayContent(buffer, 0, count);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

            EnsureBlobSuccess(response, "Upload of " + blobPath);
        }

        private static async Task BlobPutBlockList(string blobPath, List<string> blockIds, CancellationToken ct)
        {
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>");

            foreach (string blockId in blockIds)
            {
                xml.Append("<Latest>").Append(blockId).Append("</Latest>");
            }

            xml.Append("</BlockList>");

            using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(EscapeBlobPath(blobPath), "&comp=blocklist"));

            AzureAuth.AddCommonHeaders(request);

            //The content type belongs to the assembled blob, and this is the request that
            //creates it - the individual blocks have no say in it.
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-type", GuessContentType(blobPath));

            request.Content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml");

            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

            EnsureBlobSuccess(response, "Upload of " + blobPath);
        }

        /// <summary>
        /// Downloads a blob to a local file, streaming it straight to disk.
        /// </summary>
        public async Task Download(string remotePath, string localPath, Action<long> progress, CancellationToken ct)
        {
            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var request = new HttpRequestMessage(HttpMethod.Get, AzureAuth.BlobUri(EscapeBlobPath(remotePath)));
            AzureAuth.AddCommonHeaders(request);
            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            EnsureBlobSuccess(response, "Download of " + remotePath);

            using Stream content = await response.Content.ReadAsStreamAsync(ct);
            using var target = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, FileCopyBuffer, useAsync: true);

            byte[] buffer = new byte[FileCopyBuffer];

            while (true)
            {
                int read = await content.ReadAsync(buffer, ct);
                if (read == 0) break;

                await target.WriteAsync(buffer.AsMemory(0, read), ct);

                progress?.Invoke(read);
            }
        }

        // ---------------------------------------------------------------- naming operations

        /// <summary>
        /// Copies a blob inside the container. The service does the copying - the bytes never
        /// come down to us, which is what makes a move of a large file instant.
        /// </summary>
        public async Task Copy(string sourcePath, string destinationPath, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(EscapeBlobPath(destinationPath)));

            AzureAuth.AddCommonHeaders(request);

            //The source is named by URL, and the service reads it with the credentials in that
            //URL rather than the ones on the request - so the authorization has to be on it too.
            request.Headers.TryAddWithoutValidation("x-ms-copy-source", AzureAuth.BlobCopySourceUri(sourcePath));

            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

            EnsureBlobSuccess(response, "Copy of " + sourcePath);

            if (response.Headers.TryGetValues("x-ms-copy-status", out IEnumerable<string> status))
            {
                foreach (string value in status)
                {
                    if (string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase))
                    {
                        await BlobAwaitCopy(destinationPath, ct);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Waits out a copy the service decided to do in the background. Within one account a
        /// copy is normally finished by the time it answers, so this rarely runs.
        /// </summary>
        private static async Task BlobAwaitCopy(string destinationPath, CancellationToken ct)
        {
            for (int attempt = 0; attempt < 600; attempt++)
            {
                await Task.Delay(500, ct);

                using var request = new HttpRequestMessage(HttpMethod.Head, AzureAuth.BlobUri(EscapeBlobPath(destinationPath)));
                AzureAuth.AddCommonHeaders(request);
                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

                EnsureBlobSuccess(response, "Copy to " + destinationPath);

                if (!response.Headers.TryGetValues("x-ms-copy-status", out IEnumerable<string> status)) return;

                foreach (string value in status)
                {
                    if (string.Equals(value, "success", StringComparison.OrdinalIgnoreCase)) return;
                    if (string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase)) break;

                    throw new BlobFileException("Copy to " + destinationPath + " ended as " + value + ".");
                }
            }

            throw new BlobFileException("Copy to " + destinationPath + " is taking too long; it may still finish on the server.");
        }

        /// <summary>
        /// Deletes a blob - or, when <paramref name="path"/> ends in a slash, the zero-byte
        /// marker blob that stands for a folder. One that is already gone is not an error - two
        /// ways of removing the same folder can easily both reach the same file.
        /// </summary>
        public async Task Delete(string path, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, AzureAuth.BlobUri(EscapeBlobPath(path)));
            AzureAuth.AddCommonHeaders(request);
            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

            EnsureBlobSuccess(response, "Delete of " + path, allowMissing: true);
        }

        /// <summary>
        /// Writes the empty blob that stands for a folder. Without one, a folder holding no
        /// files does not exist as far as a listing is concerned.
        /// </summary>
        public async Task CreateFolder(string folderPath, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(EscapeBlobPath(folderPath)));

            AzureAuth.AddCommonHeaders(request);
            request.Headers.Add("x-ms-blob-type", "BlockBlob");

            request.Content = new ByteArrayContent(Array.Empty<byte>());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            AzureAuth.AuthorizeRequest(request);

            using HttpResponseMessage response = await fileClient.SendAsync(request, ct);

            EnsureBlobSuccess(response, "Creating folder " + folderPath);
        }

        /// <summary>
        /// Content type from the extension. Only the handful worth getting right - a browser
        /// opening the blob URL directly is the only thing that reads it.
        /// </summary>
        private static string GuessContentType(string blobPath)
        {
            string extension = Path.GetExtension(blobPath ?? string.Empty).ToLowerInvariant();

            return extension switch
            {
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".txt" or ".log" or ".md" => "text/plain",
                ".csv" => "text/csv",
                ".htm" or ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "text/javascript",
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }
    }

    /// <summary>
    /// A remote file operation the service refused. Carries a message meant to be shown as it is.
    /// </summary>
    public sealed class BlobFileException : Exception
    {
        public BlobFileException() { }

        public BlobFileException(string message) : base(message) { }

        public BlobFileException(string message, Exception innerException) : base(message, innerException) { }
    }
}
