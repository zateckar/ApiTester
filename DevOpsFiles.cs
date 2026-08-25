using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// The Azure DevOps git repository as an <see cref="IFilesBackend"/>, used when the profile
    /// syncs with DevOps - the Files tab browses the repository root the way the blob version
    /// browses the container root. The DevOps REST API is the only transport; no local clone is
    /// involved. Where the two stores differ, git's shape shows through:
    ///
    ///   - folders exist only by holding something, so an empty folder is kept by a zero-byte
    ///     ".folder" marker file, hidden in listings - the blob backend's trailing-slash marker
    ///     blob plays the same role;
    ///   - the items API reports no sizes or modification dates - those columns stay blank;
    ///   - every write is a commit: upload, marker creation and delete each push one, and a copy
    ///     (the rename/move path) fetches the content and commits it under the new name, because
    ///     git has no server-side copy;
    ///   - an upload carries its whole payload as base64 inline in the push JSON. There is no
    ///     block upload, so memory grows with the file and the service caps a push - very large
    ///     files are the blob backend's home turf.
    ///
    /// Failures surface as <see cref="BlobFileException"/>, which the Files tab already reports
    /// to its status line; nothing here logs to the sync log or touches a control.
    /// </summary>
    internal sealed class DevOpsFilesBackend : IFilesBackend
    {
        private const string ApiVersion = "api-version=7.0";

        //The file that keeps an otherwise empty folder on the listing. Created by CreateFolder,
        //deleted when the folder is deleted, hidden from both Browse and BrowseTree.
        private const string FolderMarker = ".folder";

        //Same reasoning as the blob backend's unbounded client: a transfer is bounded by the
        //Cancel button, and a 100 second default would abort a large download halfway through.
        private static readonly HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };

        private const int CopyBuffer = 256 * 1024;

        private readonly Setting settings;
        private readonly string baseAddress;

        public DevOpsFilesBackend(Setting settings)
        {
            this.settings = settings;
            baseAddress = DevOpsSyncStore.RepoApiBase(settings?.DevOpsRepo);
        }

        // ---------------------------------------------------------------- transport

        private HttpRequestMessage NewRequest(HttpMethod method, string pathAndQuery)
        {
            var request = new HttpRequestMessage(method, baseAddress + pathAndQuery);

            //Empty user name: Azure DevOps accepts any, and the token is what matters.
            request.Headers.TryAddWithoutValidation("Authorization",
                "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + (settings.DevOpsPat ?? string.Empty).Trim())));

            return request;
        }

        private static string Enc(string value)
            => Uri.EscapeDataString(value ?? string.Empty);

        /// <summary>
        /// Everything the wire knows about a refusal, as a BlobFileException the Files tab shows
        /// as it is: the status, plus DevOps' own "message" field, which names the permission it
        /// missed far better than the status does.
        /// </summary>
        private static async Task<BlobFileException> Failure(HttpResponseMessage response, string what)
        {
            var detail = new StringBuilder(what).Append(" failed: ")
                .Append((int)response.StatusCode).Append(' ').Append(response.ReasonPhrase);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                if (doc.RootElement.TryGetProperty("message", out JsonElement message)
                    && !string.IsNullOrWhiteSpace(message.GetString()))
                {
                    detail.Append(": ").Append(message.GetString().Trim());
                }
            }
            catch (Exception ex) when (ex is JsonException or ObjectDisposedException or InvalidOperationException)
            {
                //Anything but JSON still has the status line.
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                detail.Append(" The PAT needs the Code (read & write) scope on this repository.");
            }

            return new BlobFileException(detail.ToString());
        }

        private string BranchName => string.IsNullOrWhiteSpace(settings.DevOpsBranch) ? "main" : settings.DevOpsBranch;

        /// <summary>
        /// The head commit of the configured branch, or empty when the branch does not exist
        /// yet - the first push creates it, and a listing of an empty repository is a complete
        /// listing of zero entries.
        /// </summary>
        private async Task<string> BranchHead(string operation, CancellationToken ct)
        {
            //"&", not "?": this URL already has a query part. See DevOpsSyncStore.BranchHead,
            //where folding api-version into the filter once produced pushes against a
            //not-yet-existing branch - answered 409 the moment the branch actually existed.
            using var request = NewRequest(HttpMethod.Get, "refs?filter=heads/" + Enc(BranchName) + "&" + ApiVersion);

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode) throw await Failure(response, "DevOps " + operation + " (branch lookup)");

            byte[] body = await response.Content.ReadAsByteArrayAsync(ct);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("value", out JsonElement refs)
                    && refs.GetArrayLength() > 0
                    && refs[0].TryGetProperty("objectId", out JsonElement id))
                {
                    return id.GetString();
                }

                return string.Empty;
            }
            catch (JsonException ex)
            {
                throw new BlobFileException("DevOps " + operation + ": the branch lookup answered with unreadable JSON: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Query part of the items GET for one path on the configured branch. Item paths on the
        /// wire carry a leading slash - "readme.md" the store speaks in is "/readme.md" here.
        /// </summary>
        private string ItemQuery(string path)
            => "items?path=" + Enc("/" + (path ?? string.Empty).TrimStart('/'))
             + "&versionType=Branch&version=" + Enc(BranchName)
             + "&" + ApiVersion;

        /// <summary>True when the file exists on the branch; false on a plain 404.</summary>
        private async Task<bool> Exists(string path, CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Get, ItemQuery(path));

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            if (!response.IsSuccessStatusCode) throw await Failure(response, "DevOps lookup of " + path);

            return true;
        }

        // ---------------------------------------------------------------- listing

        public async Task<List<BlobFile>> Browse(string prefix, CancellationToken ct)
        {
            string self = (prefix ?? string.Empty).TrimEnd('/');

            var entries = new List<BlobFile>();

            foreach (JsonElement item in await ListItems(prefix, "OneLevel", ct))
            {
                if (!item.TryGetProperty("path", out JsonElement p)) continue;

                string name = p.GetString()?.TrimStart('/');
                if (string.IsNullOrEmpty(name)) continue;

                //The listing of a folder includes that folder itself; the browser adds its own "..".
                if (string.Equals(name, self, StringComparison.Ordinal)) continue;

                if (item.TryGetProperty("isFolder", out JsonElement isFolder) && isFolder.GetBoolean())
                {
                    entries.Add(new BlobFile { BlobPath = name + "/", IsFolder = true });
                    continue;
                }

                if (IsMarker(name)) continue;

                //Sizes and dates are not part of an items listing - both stay unknown (-1/null).
                entries.Add(new BlobFile { BlobPath = name, Length = -1 });
            }

            return entries;
        }

        public async Task<List<BlobFile>> BrowseTree(string prefix, CancellationToken ct)
        {
            var entries = new List<BlobFile>();

            foreach (JsonElement item in await ListItems(prefix, "Full", ct))
            {
                if (item.TryGetProperty("isFolder", out JsonElement isFolder) && isFolder.GetBoolean()) continue;
                if (!item.TryGetProperty("path", out JsonElement p)) continue;

                string name = p.GetString()?.TrimStart('/');
                if (string.IsNullOrEmpty(name) || IsMarker(name)) continue;

                entries.Add(new BlobFile { BlobPath = name, Length = -1 });
            }

            return entries;
        }

        private static bool IsMarker(string name)
            => name.EndsWith("/" + FolderMarker, StringComparison.Ordinal);

        /// <summary>
        /// The item entries of the scope path, cloned out of the response document. An empty
        /// repository and a prefix that names nothing are both a plain empty listing.
        /// </summary>
        private async Task<List<JsonElement>> ListItems(string prefix, string recursion, CancellationToken ct)
        {
            string branch = await BranchHead("listing", ct);

            //The branch does not exist yet: an empty repository, hence nothing under any prefix.
            if (branch.Length == 0) return new List<JsonElement>();

            string path = "/" + (prefix ?? string.Empty).TrimEnd('/');

            using var request = NewRequest(HttpMethod.Get,
                "items?scopePath=" + Enc(path)
                + "&recursionLevel=" + recursion
                + "&includeContentMetadata=false&versionType=Branch&version=" + Enc(BranchName)
                + "&" + ApiVersion);

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            //A folder that holds nothing does not exist in git - an empty listing, not an error.
            if (response.StatusCode == HttpStatusCode.NotFound) return new List<JsonElement>();
            if (!response.IsSuccessStatusCode) throw await Failure(response, "DevOps listing of " + path);

            byte[] body = await response.Content.ReadAsByteArrayAsync(ct);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);

                var items = new List<JsonElement>();

                if (doc.RootElement.TryGetProperty("value", out JsonElement value))
                {
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        items.Add(item.Clone());
                    }
                }

                return items;
            }
            catch (JsonException ex)
            {
                throw new BlobFileException("DevOps listing of " + path + " returned unreadable JSON: " + ex.Message, ex);
            }
        }

        // ---------------------------------------------------------------- transfers

        /// <summary>
        /// Uploads a local file as a single commit. The pushes API takes the payload base64
        /// inline in the JSON - there is no block upload of any kind - so memory grows with the
        /// file and the service caps a push. Progress moves while the file is read in.
        /// </summary>
        public async Task Upload(string localPath, string remotePath, Action<long> progress, CancellationToken ct)
        {
            //add creates, edit replaces; an add over an existing file (or an edit of a missing
            //one) is what the service answers with a conflict, so ask first.
            bool replace = await Exists(remotePath, ct);

            byte[] content = await ReadAll(localPath, progress, ct);

            await Push(remotePath, !replace, content, "files upload " + LastSegment(remotePath), ct);
        }

        /// <summary>
        /// The file's whole content, read in chunks so the progress bar moves while the payload
        /// is assembled.
        /// </summary>
        private static async Task<byte[]> ReadAll(string localPath, Action<long> progress, CancellationToken ct)
        {
            using var source = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBuffer, useAsync: true);
            using var memory = new MemoryStream(source.Length <= int.MaxValue ? (int)source.Length : 0);

            byte[] buffer = new byte[CopyBuffer];

            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (read == 0) break;

                memory.Write(buffer, 0, read);
                progress?.Invoke(read);
            }

            return memory.ToArray();
        }

        /// <summary>
        /// Downloads a file to disk, streaming it straight through.
        /// </summary>
        public async Task Download(string remotePath, string localPath, Action<long> progress, CancellationToken ct)
        {
            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var request = NewRequest(HttpMethod.Get, ItemQuery(remotePath));
            request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode) throw await Failure(response, "DevOps download of " + remotePath);

            using Stream content = await response.Content.ReadAsStreamAsync(ct);
            using var target = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBuffer, useAsync: true);

            byte[] buffer = new byte[CopyBuffer];

            while (true)
            {
                int read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (read == 0) break;

                await target.WriteAsync(buffer.AsMemory(0, read), ct);

                progress?.Invoke(read);
            }
        }

        // ---------------------------------------------------------------- naming operations

        /// <summary>
        /// Copies by fetching the source and committing its content under the new name - git has
        /// no server-side copy, so unlike the blob version the bytes do cross this machine.
        /// </summary>
        public async Task Copy(string sourcePath, string destinationPath, CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Get, ItemQuery(sourcePath));
            request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode) throw await Failure(response, "DevOps read of " + sourcePath);

            byte[] content = await response.Content.ReadAsByteArrayAsync(ct);

            bool replace = await Exists(destinationPath, ct);

            await Push(destinationPath, !replace, content, "files copy " + LastSegment(sourcePath), ct);
        }

        /// <summary>
        /// Deletes a file - or, when <paramref name="path"/> ends in a slash, the marker that
        /// stands for a folder. A target that is already gone is not an error.
        /// </summary>
        public async Task Delete(string path, CancellationToken ct)
        {
            //A folder is named by its trailing slash; what actually exists in git is its marker.
            string target = path.EndsWith('/') ? path + FolderMarker : path;

            if (!await Exists(target, ct)) return;

            string changes = "{\"changeType\":\"delete\",\"item\":{\"path\":" + DevOpsSyncStore.J("/" + target) + "}}";

            string branch = await BranchHead("delete", ct);

            await PostChanges(branch, changes, "files delete " + LastSegment(target), "delete of " + target, ct);
        }

        /// <summary>
        /// Creates the zero-byte marker that keeps an otherwise empty folder on the listing.
        /// One that is already there is left alone, as the blob version's marker blob is.
        /// </summary>
        public async Task CreateFolder(string folderPath, CancellationToken ct)
        {
            string marker = folderPath + FolderMarker;

            if (await Exists(marker, ct)) return;

            await Push(marker, isNewFile: true, Array.Empty<byte>(), "files folder " + folderPath.TrimEnd('/'), ct);
        }

        // ---------------------------------------------------------------- git pushes

        private static string LastSegment(string path)
        {
            int slash = (path ?? string.Empty).LastIndexOf('/');
            return slash < 0 ? path : path[(slash + 1)..];
        }

        /// <summary>
        /// One commit creating or replacing a single file. Where the sync's push carries a
        /// .meta sidecar per row, a plain file needs no such thing - one change per commit.
        /// </summary>
        private async Task Push(string path, bool isNewFile, byte[] content, string comment, CancellationToken ct)
        {
            string branch = await BranchHead("push", ct);

            //changeType add creates; edit replaces. On an empty repository every file is an add.
            string changeType = isNewFile || branch.Length == 0 ? "add" : "edit";

            string changes = "{\"changeType\":\"" + changeType
                + "\",\"item\":{\"path\":" + DevOpsSyncStore.J("/" + path)
                + "},\"newContent\":{\"contentType\":\"base64Encoded\",\"content\":"
                + DevOpsSyncStore.J(Convert.ToBase64String(content ?? Array.Empty<byte>())) + "}}";

            await PostChanges(branch, changes, comment, "push of " + path, ct);
        }

        private async Task PostChanges(string branchHead, string changesJson, string comment, string what, CancellationToken ct)
        {
            //The all-zeros id is how the API spells "no such commit yet" - the push that creates
            //the branch has no oldObjectId to point at, and an empty string is a 400 there.
            string oldObjectId = branchHead.Length == 0 ? "0000000000000000000000000000000000000000" : branchHead;

            string body = "{\"refUpdates\":[{\"name\":" + DevOpsSyncStore.J("refs/heads/" + BranchName)
                + ",\"oldObjectId\":\"" + oldObjectId + "\"}],"
                + "\"commits\":[{\"comment\":" + DevOpsSyncStore.J(comment) + ",\"changes\":[" + changesJson + "]}]}";

            using var request = NewRequest(HttpMethod.Post, "pushes?" + ApiVersion);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode) return;

            //The branch head moved between the lookup and this commit: somebody else pushed
            //first. Retrying the operation is the whole remedy, so say so.
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new BlobFileException("DevOps " + what + ": another commit landed first; try again.");
            }

            throw await Failure(response, "DevOps " + what);
        }
    }
}
