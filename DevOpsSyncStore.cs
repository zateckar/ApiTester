using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// An Azure DevOps git repository as an <see cref="ISyncStore"/>. Layout in the repository,
    /// under the same prefix the blob version would use:
    ///   rows/{uid}      - encrypted session body (what a blob's content is there)
    ///   rows/{uid}.meta - {"v":1,"m":{...}} JSON holding what blob metadata would hold
    ///   dels/, ticks/   - tombstone and tick bodies as blobs; their seq lives in a .meta file
    /// No blob metadata exists in a git repository, so the pull reads one extra small file per
    /// session; the DevOps REST API is the only transport, no local clone is involved.
    ///
    /// Confidentiality note: everything this store writes - row bodies, metadata files,
    /// tombstones, ticks - is ciphertext when an encryption key is set, which the sync requires
    /// for DevOps. The repository learns file names and timestamps only.
    /// </summary>
    internal sealed class DevOpsSyncStore : ISyncStore
    {
        private const string ApiVersion = "?api-version=7.0";

        //Blob bodies of individual ticks and tombstones are tiny; the base64 and the JSON that
        //carries them enlarge them, and the push commits hold at most a handful of them after
        //the first sync. Two requests per changed object is fine here and keeps the code simple.
        private static readonly HttpClient client = new();

        private readonly Form1 host;

        private readonly Setting settings;
        private readonly string baseAddress;

        public DevOpsSyncStore(Form1 host, Setting settings)
        {
            this.host = host;
            this.settings = settings;

            baseAddress = RepoApiBase(settings.DevOpsRepo);
        }

        /// <summary>
        /// "https://host/projects/{teamProject}/_apis/git/repositories/{repo}/" from the repo's
        /// web URL - "host/projects/{teamProject}/_git/{repo}", pasted from the browser, is what
        /// people naturally have. The REST API lives under "_apis/git/repositories/" at the same
        /// project path, so "_git" is swapped for it. Shared with the Files tab's DevOps backend.
        /// </summary>
        internal static string RepoApiBase(string repoUrl)
        {
            string repo = (repoUrl ?? string.Empty).Trim();

            foreach (string scheme in new[] { "https://", "http://" })
            {
                if (repo.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) repo = repo.Substring(scheme.Length);
            }

            int pivot = repo.IndexOf("/_git/", StringComparison.OrdinalIgnoreCase);
            string apiPath = pivot < 0
                ? repo
                : string.Concat(repo.AsSpan(0, pivot), "/_apis/git/repositories/", repo.AsSpan(pivot + "/_git/".Length));

            return "https://" + apiPath.Trim('/', ' ') + "/";
        }

        // ---------------------------------------------------------------- transport

        private HttpRequestMessage NewRequest(HttpMethod method, string pathAndQuery)
        {
            var request = new HttpRequestMessage(method, baseAddress + pathAndQuery);

            //Empty user name: Azure DevOps accepts any, and the token is what matters.
            request.Headers.TryAddWithoutValidation("Authorization",
                "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + settings.DevOpsPat?.Trim())));

            return request;
        }

        /// <summary>
        /// What the wire gave us, on one line. DevOps answers errors with a JSON body holding a
        /// "message" field, which names the permission it missed far better than the status.
        /// </summary>
        private static async Task<string> FailureDetail(HttpResponseMessage response, string what)
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

            return detail.ToString();
        }

        /// <summary>
        /// Query part of the items GET for one path on this setting's branch.
        /// </summary>
        private string ItemQuery(string path)
            => "items?path=" + Enc(path)
             + "&versionType=Branch&version=" + Enc(settings.DevOpsBranch)
             + "&api-version=7.0";

        private static string Enc(string value)
            => Uri.EscapeDataString(value ?? string.Empty);

        /// <summary>
        /// A string as a JSON string literal (quotes and escaping included), for the push
        /// bodies built by hand below. Through the writer rather than JsonSerializer: the
        /// AOT build has no reflection-based serialization to call. JsonEncodedText.Encode is
        /// not the alternative it looks like - its ToString returns the original text
        /// untouched, which is what once put bare words where the API expected quoted strings.
        /// Shared with the Files tab's DevOps backend, whose push bodies are built the same way.
        /// </summary>
        internal static string J(string value)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) writer.WriteStringValue(value ?? string.Empty);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>The guid refs/nodes call for the branch the sync works, or null.</summary>
        private async Task<string> BranchHead(string operation)
        {
            try
            {
                //"&", not the constant's own "?": this URL already has a query part. A second
                //"?" folds api-version into the filter value, the server then matches no ref,
                //and every push offers the all-zeros oldObjectId - answered with 409 TF401028
                //the moment the branch actually exists.
                using var request = NewRequest(HttpMethod.Get,
                    "refs?filter=heads/" + Enc(settings.DevOpsBranch) + "&" + ApiVersion.TrimStart('?'));

                using HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    host.SyncOpResult(ok: false);
                    host.StoreLog("DevOps " + operation + " failed: " + await FailureDetail(response, "branch lookup"));
                    return null;
                }

                using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());

                host.SyncOpResult(ok: true);

                if (doc.RootElement.TryGetProperty("value", out JsonElement refs)
                    && refs.GetArrayLength() > 0
                    && refs[0].TryGetProperty("objectId", out JsonElement id))
                {
                    return id.GetString();
                }

                //Not an error the round should block on: an empty repository is a valid state -
                //the first push just starts the branch. Logged because anything else means a
                //typo in the branch name.
                host.StoreLog("DevOps " + operation + ": the \"" + settings.DevOpsBranch + "\" branch does not exist yet; it is created by the first push.");

                return string.Empty;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("DevOps " + operation + " failed: " + ex.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- reads

        public async Task<byte[]> Get(string path)
        {
            //The interface flattens "missing" and "failed" into one null; callers that must
            //tell them apart (ReadMeta) use GetItem instead.
            (bool ok, byte[] content) = await GetItem(path);
            return ok ? content : null;
        }

        /// <summary>The content, with "the file is absent" told apart from "the read failed".</summary>
        private async Task<(bool Ok, byte[] Content)> GetItem(string path)
        {
            try
            {
                using var request = NewRequest(HttpMethod.Get, ItemQuery(path));

                using HttpResponseMessage response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    host.SyncOpResult(ok: true);
                    return (true, null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    host.SyncOpResult(ok: false);
                    host.StoreLog(await FailureDetail(response, "DevOps read of " + path));
                    return (false, null);
                }

                host.SyncOpResult(ok: true);

                return (true, await response.Content.ReadAsByteArrayAsync());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("DevOps read of " + path + " failed: " + ex.Message);
                return (false, null);
            }
        }

        public async Task<List<SyncEntry>> List(string prefix, bool includeMetadata)
        {
            string branch = await BranchHead("listing");
            if (branch is null) return null;

            if (branch.Length == 0)
            {
                //Empty repository: a complete listing of zero entries.
                return new List<SyncEntry>();
            }

            string path = "/" + prefix.TrimEnd('/');

            try
            {
                using var request = NewRequest(HttpMethod.Get,
                    "items?scopePath=" + Enc(path)
                    + "&recursionLevel=Full&includeContentMetadata=false&versionType=Branch&version=" + Enc(settings.DevOpsBranch)
                    + "&" + ApiVersion.TrimStart('?'));

                using HttpResponseMessage response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    //The prefix folder does not exist yet - nothing published under it.
                    host.SyncOpResult(ok: true);
                    return new List<SyncEntry>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    host.SyncOpResult(ok: false);
                    host.StoreLog(await FailureDetail(response, "DevOps listing of " + prefix));
                    return null;
                }

                using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());

                var entries = new List<SyncEntry>();

                if (doc.RootElement.TryGetProperty("value", out JsonElement items))
                {
                    foreach (JsonElement item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty("isFolder", out JsonElement isFolder) || isFolder.GetBoolean()) continue;
                        if (!item.TryGetProperty("path", out JsonElement p)) continue;

                        string fullPath = p.GetString();

                        //Listing yields "/apiproxy-iis/rows/abc"; the store speaks "apiproxy-iis/rows/abc".
                        string name = fullPath.TrimStart('/');

                        //The metadata file is a sibling of its object, not an entry in its own right.
                        if (name.EndsWith(".meta", StringComparison.Ordinal)) continue;

                        Dictionary<string, string> metadata = null;

                        if (includeMetadata)
                        {
                            metadata = await ReadMeta(name);
                            if (metadata is null) return null;
                        }

                        entries.Add(new SyncEntry { Name = name, Metadata = metadata });
                    }
                }

                host.SyncOpResult(ok: true);
                return entries;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("DevOps listing of " + prefix + " failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// <returns>The decoded metadata dictionary, an empty one for a missing file,
        /// null when the read itself failed (the round must see that, not gamble on it).</returns>
        /// </summary>
        private async Task<Dictionary<string, string>> ReadMeta(string name)
        {
            (bool ok, byte[] raw) = await GetItem(name + ".meta");

            //Only a failed read ends the listing. A missing .meta - an object written by an
            //older build, or a hand-edited repository - is no metadata, not a broken round.
            if (!ok) return null;
            if (raw is null) return new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(raw);

                var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

                if (doc.RootElement.TryGetProperty("m", out JsonElement m))
                {
                    foreach (JsonProperty pair in m.EnumerateObject())
                    {
                        metadata[pair.Name] = pair.Value.GetString();
                    }
                }

                return metadata;
            }
            catch (JsonException ex)
            {
                host.StoreLog("DevOps metadata of " + name + " is unreadable: " + ex.Message);
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        // ---------------------------------------------------------------- writes

        /// <summary>
        /// <see cref="ISyncStore.Put"/>. Row objects are immutable, so refusing to overwrite is
        /// the ordinary case; onlyIfMissing therefore means Exists, not failure. Ticks and
        /// tombstones are called with false and simply replace what is there.
        /// </summary>
        public async Task<SyncStoreResult> Put(string path, byte[] content, IReadOnlyDictionary<string, string> metadata, bool onlyIfMissing)
        {
            //A GET cannot distinguish 404 (absent) from a failed read in this store; for the
            //immutability check all that matters is "came back with content".
            byte[] existing = await Get(path);

            if (existing is not null && onlyIfMissing) return SyncStoreResult.Exists;

            return await Push(path, existing is null, content, metadata, "sync " + LastSegment(path));
        }

        public async Task<SyncStoreResult> PutMetadata(string path, IReadOnlyDictionary<string, string> metadata)
        {
            //A metadata file without its body has never been a valid state; leaving the body's
            //existence check to the push's response.
            return await Push(path, false, null, metadata, "sync meta " + LastSegment(path));
        }

        public async Task<SyncStoreResult> Delete(string path)
        {
            byte[] existing = await Get(path);

            if (existing is null)
            {
                //Missing already - the same ok the blob delete gives an absent blob.
                return SyncStoreResult.Ok;
            }

            return await PushDelete(path);
        }

        // ---------------------------------------------------------------- git pushes

        private static string LastSegment(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path[(slash + 1)..];
        }

        private static string B64(byte[] content) => Convert.ToBase64String(content ?? Array.Empty<byte>());

        private static string MetaJson(IReadOnlyDictionary<string, string> metadata)
        {
            var builder = new StringBuilder("{\"v\":1,\"m\":{");
            bool first = true;

            foreach (var (key, value) in metadata ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrEmpty(value)) continue;

                if (!first) builder.Append(',');
                first = false;

                builder.Append(J(key)).Append(':').Append(J(value));
            }

            return builder.Append("}}").ToString();
        }

        /// <summary>
        /// One commit replacing the body's file and its .meta. A null body only rewrites the
        /// metadata file, for the note/group edit path - PutMetadata costs an edit of a tiny
        /// JSON file rather than an upload of the whole session.
        /// </summary>
        private async Task<SyncStoreResult> Push(string path, bool isNewFile, byte[] content, IReadOnlyDictionary<string, string> metadata, string comment)
        {
            string branch = await BranchHead("push");
            if (branch is null) return SyncStoreResult.Failed;

            //changeType add creates; edit replaces. On an empty repository every file is an add.
            //Between the preview and the commit a conflict loses this one push: PostChanges
            //reports it as Conflict rather than Failed, so the row stays dirty and the next
            //round retries it without tripping the round's failure breaker - acceptable for
            //what is effectively a single-user-per-row workload.
            string changeType = isNewFile || branch.Length == 0 ? "add" : "edit";

            var changes = new StringBuilder();

            if (content is not null)
            {
                changes.Append("{\"changeType\":\"").Append(changeType)
                    .Append("\",\"item\":{\"path\":").Append(J("/" + path))
                    .Append("},\"newContent\":{\"contentType\":\"base64Encoded\",\"content\":")
                    .Append(J(B64(content))).Append("}},");
            }

            changes.Append("{\"changeType\":\"").Append(changeType)
                .Append("\",\"item\":{\"path\":").Append(J("/" + path + ".meta"))
                .Append("},\"newContent\":{\"contentType\":\"rawtext\",\"content\":")
                .Append(J(MetaJson(metadata))).Append("}}");

            return await PostChanges(branch, changes.ToString(), comment, "push of " + path);
        }

        private async Task<SyncStoreResult> PushDelete(string path)
        {
            string branch = await BranchHead("delete");
            if (branch is null || branch.Length == 0) return SyncStoreResult.Failed;

            string changes = "{\"changeType\":\"delete\",\"item\":{\"path\":" + J("/" + path) + "}},"
                + "{\"changeType\":\"delete\",\"item\":{\"path\":" + J("/" + path + ".meta") + "}}";

            return await PostChanges(branch, changes, "sync delete " + LastSegment(path), "delete of " + path);
        }

        private async Task<SyncStoreResult> PostChanges(string branchHead, string changesJson, string comment, string what)
        {
            //The all-zeros id is how the API spells "no such commit yet" - the push that creates
            //the branch has no oldObjectId to point at, and an empty string is a 400 there.
            string oldObjectId = branchHead.Length == 0 ? "0000000000000000000000000000000000000000" : branchHead;

            string body = "{\"refUpdates\":[{\"name\":" + J("refs/heads/" + settings.DevOpsBranch)
                + ",\"oldObjectId\":\"" + oldObjectId + "\"}],"
                + "\"commits\":[{\"comment\":" + J(comment) + ",\"changes\":[" + changesJson + "]}]}";

            try
            {
                using var request = NewRequest(HttpMethod.Post, "pushes" + ApiVersion);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    host.SyncOpResult(ok: true);
                    return SyncStoreResult.Ok;
                }

                //The branch head moved between the lookup and this commit (409, TF401028):
                //another instance pushed first. That is a race to retry next round, not an
                //error - reporting it as Failed would shut the automatic sync off.
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    host.SyncOpResult(ok: true);
                    host.StoreLog("DevOps " + what + ": another push landed first; the next round retries it.");
                    return SyncStoreResult.Conflict;
                }

                host.SyncOpResult(ok: false);
                host.StoreLog(await FailureDetail(response, "DevOps " + what));
                return SyncStoreResult.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("DevOps " + what + " failed: " + ex.Message);
                return SyncStoreResult.Failed;
            }
        }
    }
}
