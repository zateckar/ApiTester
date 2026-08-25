using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace ApiTester
{

    public partial class Form1 : Form
    {
        internal const string BlobApiVersion = "2021-12-02";

        //The store this profile syncs against - captured at the start of the round so a settings
        //edit cannot swap the backend underneath a running one.
        private ISyncStore activeSyncStore;

        /// <summary>How the running sync hears about it: any store operation's outcome.</summary>
        internal void SyncOpResult(bool ok) => SyncOperationResult(ok);

        /// <summary>
        /// The store this profile syncs against. DevOps when it was chosen and its settings are
        /// complete, the blob container otherwise - an incomplete DevOps setup never downgrades
        /// the sync to the container behind the user's back, it just stays unconfigured.
        /// </summary>
        private ISyncStore SyncStoreForThisRun
        {
            get
            {
                if (Form1.CurrentSettings.SyncWithDevOps)
                {
                return string.IsNullOrWhiteSpace(Form1.CurrentSettings.DevOpsPat) || string.IsNullOrWhiteSpace(Form1.CurrentSettings.DevOpsRepo)
                    ? null
                    : new DevOpsSyncStore(this, Form1.CurrentSettings);
                }

                return new AzureBlobStore(this);
            }
        }

        /// <summary>
        /// True when the profile carries enough information to talk to the store that is
        /// selected for it.
        /// </summary>
        /// <param name="complain">Whether to say so in the log. The automatic sync stays quiet -
        /// an unconfigured profile is a normal state, not an error.</param>
        private bool BlobConfigured(bool complain)
        {
            string missing = null;

            if (Form1.CurrentSettings.SyncWithDevOps)
            {
                if (string.IsNullOrWhiteSpace(Form1.CurrentSettings.DevOpsPat) || string.IsNullOrWhiteSpace(Form1.CurrentSettings.DevOpsRepo))
                    missing = "DevOps PAT and repository must be set in Settings.";
            }
            else if (string.IsNullOrWhiteSpace(Form1.CurrentSettings.BlobStorageAccount)
                     || string.IsNullOrWhiteSpace(Form1.CurrentSettings.BlobContainer)
                     || (string.IsNullOrWhiteSpace(Form1.CurrentSettings.BlobSASToken) && string.IsNullOrWhiteSpace(Form1.CurrentSettings.BlobAccountKey)))
            {
                missing = "Blob storage account, container and either a SAS token or an account key must be set in Settings.";
            }

            if (missing is null) return true;

            if (complain) listBox_blob.Items.Insert(0, missing);

            return false;
        }

        /// <summary>
        /// What the "List blobs" button shows: the instances taking part, and how many session
        /// objects the remote holds for this profile.
        /// </summary>
        private async Task GetBlobList()
        {
            string prefix = SyncPrefix();

            ISyncStore store = activeSyncStore = SyncStoreForThisRun;

            var ticks = await store.List(prefix + "/" + TicksFolder + "/", includeMetadata: true);
            var rows = await store.List(prefix + "/" + RowsFolder + "/", includeMetadata: false);

            listBox_blob.Items.Insert(0, "Namespace: " + prefix);
            listBox_blob.Items.Insert(0, rows is null ? "Sessions in remote store: unknown" : "Sessions in remote store: " + rows.Count.ToString(CultureInfo.CurrentCulture));

            if (ticks is null) return;

            string self = await InstanceId();

            foreach (SyncEntry tick in ticks)
            {
                string id = tick.Name.Substring(tick.Name.LastIndexOf('/') + 1);

                listBox_blob.Items.Insert(0, "Instance " + id + (string.Equals(id, self, StringComparison.OrdinalIgnoreCase) ? " (this one)" : "")
                    + " - changes pushed: " + (tick.Meta(MetaSeq) ?? "?"));
            }
        }
    }

    /// <summary>
    /// URL building and request signing. Both the session sync and the Files tab build their
    /// requests here, and in Shared Key mode both route the signature through
    /// <see cref="AuthorizeRequest"/> - whose job it is to come last, after every header the
    /// signature covers is on the request.
    /// </summary>
    internal static class AzureAuth
    {
        /// <summary>
        /// True when the profile authenticates with the account key rather than a SAS. The key
        /// goes in the Authorization header, which is why it survives a proxy that rewrites
        /// URLs - the case Zscaler makes of a SAS.
        /// </summary>
        public static bool UseSharedKey => !string.IsNullOrWhiteSpace(Form1.CurrentSettings.BlobAccountKey);

        /// <summary>
        /// Build a blob URL. With a SAS the credentials ride in the query as they always have;
        /// with a Shared Key the URL carries nothing - <see cref="AuthorizeRequest"/> adds the
        /// Authorization header instead.
        /// </summary>
        public static string BlobUri(string path, string extraQuery = "")
        {
            string address = $"https://{Form1.CurrentSettings.BlobStorageAccount}.blob.core.windows.net/{Form1.CurrentSettings.BlobContainer}/{path}";

            return UseSharedKey
                ? address + (extraQuery.Length == 0 ? string.Empty : "?" + extraQuery.TrimStart('&'))
                : address + "?" + SasToken() + extraQuery;
        }

        public static string ContainerUri(string query)
        {
            string address = $"https://{Form1.CurrentSettings.BlobStorageAccount}.blob.core.windows.net/{Form1.CurrentSettings.BlobContainer}";

            return UseSharedKey
                ? address + "?" + (query ?? string.Empty).TrimStart('&')
                : address + "?" + SasToken() + query;
        }

        /// <summary>
        /// The token as a query fragment. The portal's copy sometimes includes the leading
        /// "?", which would otherwise produce "??sv=" - a first parameter named "?sv" that
        /// the service rejects with a signature error pointing nowhere near the cause.
        /// </summary>
        private static string SasToken()
            => (Form1.CurrentSettings.BlobSASToken ?? string.Empty).TrimStart('?', '&');

        /// <summary>
        /// Does the SAS or Shared Key do the talking, and stamps the request with its date and
        /// API version. Call it before the specific headers, which may join the signature.
        /// </summary>
        public static void AddCommonHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("x-ms-date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
            request.Headers.Add("x-ms-version", Form1.BlobApiVersion);
        }

        /// <summary>
        /// Signs the request as Shared Key and sets the Authorization header. No-op in SAS mode.
        /// Call it last: every header that belongs to the signature - x-ms-*, content type,
        /// content length, If-None-Match - must already be on the request.
        /// </summary>
        public static void AuthorizeRequest(HttpRequestMessage request)
        {
            if (!UseSharedKey) return;

            string signature = ComputeSharedKeySignature(request);

            request.Headers.TryAddWithoutValidation("Authorization",
                "SharedKey " + Form1.CurrentSettings.BlobStorageAccount + ":" + signature);
        }

        /// <summary>
        /// The HMAC-SHA256 over the service's string-to-sign, as of the service version this
        /// client speaks. Empty slots correspond to fields the request does not set; the service
        /// recreates the string from the request it received, so anything substituted by
        /// middleware breaks the signature just as surely as a wrong key does.
        /// </summary>
        private static string ComputeSharedKeySignature(HttpRequestMessage request)
        {
            var sign = new StringBuilder();

            sign.Append(request.Method.Method).Append('\n');

            //Content-Encoding, Content-Language - never set by this client.
            sign.Append('\n').Append('\n');

            long contentLength = request.Content?.Headers?.ContentLength ?? 0;
            sign.Append(contentLength == 0 ? string.Empty : contentLength.ToString(CultureInfo.InvariantCulture)).Append('\n');

            //Content-MD5.
            sign.Append('\n');

            sign.Append(request.Content?.Headers?.ContentType?.ToString() ?? string.Empty).Append('\n');

            //Date, If-Modified-Since, If-Match, If-None-Match, If-Unmodified-Since.
            sign.Append('\n').Append('\n').Append('\n');
            sign.Append(request.Headers.IfNoneMatch.Count == 0
                ? string.Empty
                : string.Join(",", request.Headers.IfNoneMatch.Select(t => t.ToString()))).Append('\n');
            sign.Append('\n');

            //Range.
            sign.Append('\n');

            sign.Append(CanonicalizedHeaders(request));
            sign.Append(CanonicalizedResource(request));

            byte[] key = Convert.FromBase64String(Form1.CurrentSettings.BlobAccountKey.Trim());

            using var hmac = new HMACSHA256(key);

            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sign.ToString())));
        }

        /// <summary>
        /// x-ms-* headers, names lowercase and sorted, values trimmed of folded whitespace.
        /// </summary>
        private static string CanonicalizedHeaders(HttpRequestMessage request)
        {
            var buckets = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            void Collect(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            {
                foreach (var (name, values) in headers)
                {
                    if (!name.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase)) continue;

                    string key = name.ToLowerInvariant();

                    if (!buckets.TryGetValue(key, out List<string> list)) list = buckets[key] = new List<string>();

                    list.AddRange(values.Select(v => v.Replace("\r\n", string.Empty, StringComparison.Ordinal).TrimStart()));
                }
            }

            Collect(request.Headers);

            if (request.Content is not null) Collect(request.Content.Headers);

            var result = new StringBuilder();

            foreach (var (name, values) in buckets)
            {
                result.Append(name).Append(':').Append(string.Join(",", values)).Append('\n');
            }

            return result.ToString();
        }

        /// <summary>
        /// "/account/path" plus the query, keys sorted and decoded, multi-values comma-joined.
        /// Uses the request's own URI, so whatever the callers appended is signed exactly.
        /// </summary>
        private static string CanonicalizedResource(HttpRequestMessage request)
        {
            var result = new StringBuilder("/" + Form1.CurrentSettings.BlobStorageAccount + request.RequestUri.AbsolutePath);

            string query = request.RequestUri.Query;
            if (query.Length == 0) return result.ToString();

            var buckets = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');

                string name = Uri.UnescapeDataString(equals < 0 ? pair : pair.Substring(0, equals)).ToLowerInvariant();
                string value = equals < 0 ? string.Empty : Uri.UnescapeDataString(pair[(equals + 1)..]);

                if (!buckets.TryGetValue(name, out List<string> list)) list = buckets[name] = new List<string>();

                list.Add(value);
            }

            foreach (var (name, values) in buckets)
            {
                values.Sort(StringComparer.Ordinal);
                result.Append('\n').Append(name).Append(':').Append(string.Join(",", values));
            }

            return result.ToString();
        }

        /// <summary>
        /// The URL a copy reads its source from. Unlike the request itself, the source gets no
        /// Authorization header - the service fetches it in its own name - so in Shared Key mode
        /// the URL has to carry its own authorization: a short-lived ad-hoc SAS signed with the
        /// account key.
        /// </summary>
        public static string BlobCopySourceUri(string path)
        {
            if (!UseSharedKey) return BlobUri(path);

            string account = Form1.CurrentSettings.BlobStorageAccount;
            string escapedPath = "/" + Form1.CurrentSettings.BlobContainer + "/" + path;

            string start = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            string expiry = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            //Ad-hoc read SAS on this one blob. No IP range, no protocol restriction, no stored
            //access policy, no response-header overrides - every empty field still takes a line.
            string sign = "r\n" + start + "\n" + expiry + "\n"
                + "/blob/" + account + escapedPath + "\n"
                + "\n" + Form1.BlobApiVersion + "\n\n\n\n\n\n\n";

            byte[] key = Convert.FromBase64String(Form1.CurrentSettings.BlobAccountKey.Trim());

            using var hmac = new HMACSHA256(key);

            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sign)));

            return "https://" + account + ".blob.core.windows.net" + escapedPath
                + "?sv=" + Form1.BlobApiVersion
                + "&st=" + Uri.EscapeDataString(start)
                + "&se=" + Uri.EscapeDataString(expiry)
                + "&sr=b&sp=r&sig=" + Uri.EscapeDataString(signature);
        }
    }

    /// <summary>
    /// The blob container as an <see cref="ISyncStore"/> - the backend the sync always had.
    /// </summary>
    internal sealed class AzureBlobStore : ISyncStore
    {
        //Its own client rather than the file tab's: a transfer is bounded by the user cancelling
        //it, and the shared client's per-round traffic keeps the default pool size adequate.
        private static readonly HttpClient client = new();

        private readonly Form1 host;

        public AzureBlobStore(Form1 host) => this.host = host;

        public async Task<SyncStoreResult> Put(string path, byte[] content, IReadOnlyDictionary<string, string> metadata, bool onlyIfMissing)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(path));

                AzureAuth.AddCommonHeaders(request);
                request.Headers.Add("x-ms-blob-type", "BlockBlob");
                AddMetadataHeaders(request, metadata);

                if (onlyIfMissing) request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);

                request.Content = new ByteArrayContent(content ?? Array.Empty<byte>());
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Created) { host.SyncOpResult(ok: true); return SyncStoreResult.Ok; }

                //Somebody else wrote this blob first. Row blobs are immutable, so its content is
                //what we were about to write anyway.
                if (onlyIfMissing && (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.PreconditionFailed))
                {
                    host.SyncOpResult(ok: true);
                    return SyncStoreResult.Exists;
                }

                host.SyncOpResult(ok: false);
                host.StoreLog("Upload of " + path + " failed: " + await BlobFailureDetail(response));
                return SyncStoreResult.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("Upload of " + path + " failed: " + ex.Message);
                return SyncStoreResult.Failed;
            }
        }

        /// <summary>
        /// Replaces a blob's metadata without touching its content - how an edited note or
        /// group is published, at a few hundred bytes rather than the whole session.
        /// </summary>
        public async Task<SyncStoreResult> PutMetadata(string path, IReadOnlyDictionary<string, string> metadata)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, AzureAuth.BlobUri(path, "&comp=metadata"));

                AzureAuth.AddCommonHeaders(request);
                AddMetadataHeaders(request, metadata);
                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.OK) { host.SyncOpResult(ok: true); return SyncStoreResult.Ok; }
                if (response.StatusCode == HttpStatusCode.NotFound) { host.SyncOpResult(ok: true); return SyncStoreResult.Missing; }

                host.SyncOpResult(ok: false);
                host.StoreLog("Metadata update of " + path + " failed: " + await BlobFailureDetail(response));
                return SyncStoreResult.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("Metadata update of " + path + " failed: " + ex.Message);
                return SyncStoreResult.Failed;
            }
        }

        public async Task<SyncStoreResult> Delete(string path)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, AzureAuth.BlobUri(path));
                AzureAuth.AddCommonHeaders(request);
                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await client.SendAsync(request);

                //Already gone counts as deleted.
                if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK or HttpStatusCode.NotFound)
                {
                    host.SyncOpResult(ok: true);
                    return SyncStoreResult.Ok;
                }

                host.SyncOpResult(ok: false);
                host.StoreLog("Delete of " + path + " failed: " + await BlobFailureDetail(response));
                return SyncStoreResult.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("Delete of " + path + " failed: " + ex.Message);
                return SyncStoreResult.Failed;
            }
        }

        public async Task<byte[]> Get(string path)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, AzureAuth.BlobUri(path));
                AzureAuth.AddCommonHeaders(request);
                AzureAuth.AuthorizeRequest(request);

                using HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    host.SyncOpResult(ok: false);
                    host.StoreLog("Download of " + path + " failed: " + await BlobFailureDetail(response));
                    return null;
                }

                host.SyncOpResult(ok: true);
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                host.SyncOpResult(ok: false);
                host.StoreLog("Download of " + path + " failed: " + ex.Message);
                return null;
            }
        }

        public async Task<List<SyncEntry>> List(string prefix, bool includeMetadata)
        {
            var entries = new List<SyncEntry>();
            string marker = null;

            do
            {
                string query = "&restype=container&comp=list&prefix=" + Uri.EscapeDataString(prefix)
                    + (includeMetadata ? "&include=metadata" : string.Empty)
                    + (string.IsNullOrEmpty(marker) ? string.Empty : "&marker=" + Uri.EscapeDataString(marker));

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, AzureAuth.ContainerUri(query));
                    AzureAuth.AddCommonHeaders(request);
                    AzureAuth.AuthorizeRequest(request);

                    using HttpResponseMessage response = await client.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        host.SyncOpResult(ok: false);
                        host.StoreLog("Listing " + prefix + " failed: " + await BlobFailureDetail(response));
                        return null;
                    }

                    host.SyncOpResult(ok: true);

                    var xmlDoc = new XmlDocument { XmlResolver = null };
                    using (var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync(),
                                                         new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit }))
                    {
                        xmlDoc.Load(reader);
                    }

                    XmlNodeList blobNodes = xmlDoc.SelectNodes("/EnumerationResults/Blobs/Blob");

                    foreach (XmlNode blobNode in blobNodes ?? (XmlNodeList)xmlDoc.CreateDocumentFragment().ChildNodes)
                    {
                        string name = blobNode.SelectSingleNode("Name")?.InnerText;
                        if (string.IsNullOrEmpty(name)) continue;

                        entries.Add(new SyncEntry { Name = name, Metadata = ReadMetadata(blobNode) });
                    }

                    marker = xmlDoc.SelectSingleNode("/EnumerationResults/NextMarker")?.InnerText;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
                {
                    host.SyncOpResult(ok: false);
                    host.StoreLog("Listing " + prefix + " failed: " + ex.Message);
                    return null;
                }
            }
            while (!string.IsNullOrEmpty(marker));

            return entries;
        }

        /// <summary>
        /// Metadata values travel in HTTP headers, so they have to be ASCII and single line.
        /// Notes are neither, hence base64. An empty value is left out entirely - an absent
        /// key and an empty one mean the same thing to the reader.
        /// </summary>
        private static void AddMetadataHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> metadata)
        {
            if (metadata is null) return;

            foreach (var (key, value) in metadata)
            {
                if (string.IsNullOrEmpty(value)) continue;

                request.Headers.TryAddWithoutValidation("x-ms-meta-" + key, value);
            }
        }

        private static Dictionary<string, string> ReadMetadata(XmlNode blobNode)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            XmlNode container = blobNode.SelectSingleNode("Metadata");
            if (container is null) return metadata;

            foreach (XmlNode item in container.ChildNodes)
            {
                metadata[item.LocalName] = item.InnerText;
            }

            return metadata;
        }

        internal static string Status(HttpResponseMessage response)
            => (int)response.StatusCode + " " + response.ReasonPhrase;

        /// <summary>
        /// Everything the service knows about why a request failed, on one line: the status,
        /// the x-ms-error-code that tells a wrong SAS apart from an expired one, and the
        /// free-text message from the response body, which names the missing permission or
        /// the field of the signature it objected to.
        /// </summary>
        internal static async Task<string> BlobFailureDetail(HttpResponseMessage response)
        {
            var detail = new StringBuilder(Status(response));

            if (response.Headers.TryGetValues("x-ms-error-code", out IEnumerable<string> codes))
            {
                detail.Append(" (").Append(string.Join(", ", codes)).Append(')');
            }

            //A 403 is decided before any blob content is read, so encryption keys are not the
            //cause; spelling that out saves looking in the wrong place.
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                detail.Append(AzureAuth.UseSharedKey
                    ? " The account-key signature was rejected (check x-ms-date clock skew and the key itself). This happens before any content is read, so it is not an encryption key problem."
                    : " The SAS token was rejected (check its permissions and expiry, or a URL-rewriting proxy). This happens before any content is read, so it is not an encryption key problem.");
            }

            try
            {
                string message = XmlElement(await response.Content.ReadAsStringAsync().ConfigureAwait(false), "Message");

                if (!string.IsNullOrWhiteSpace(message)) detail.Append(": ").Append(message.Trim());
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                //The status line above still says enough; the body detail must never throw.
            }

            return detail.ToString();
        }

        /// <summary>
        /// Reads one element out of an Azure error body without a full XML parse - anything
        /// smarter never gets called on a healthy response, and must not fail on its own here.
        /// </summary>
        private static string XmlElement(string xml, string name)
        {
            if (string.IsNullOrEmpty(xml)) return null;

            string open = "<" + name + ">";
            int start = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += open.Length;

            int end = xml.IndexOf("</" + name + ">", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return null;

            return WebUtility.HtmlDecode(xml.Substring(start, end - start));
        }
    }
}
