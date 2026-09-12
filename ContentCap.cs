using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// LoadIntoBufferAsync with a ceiling. The response body has to be in memory before
    /// SaveSession compresses it, but an unbounded endpoint should not be able to
    /// materialize gigabytes as a byte[], a UTF-16 string, and a compressed copy.
    /// </summary>
    internal static class ContentCap
    {
        public const long MaxResponseBodyBytes = 64L * 1024 * 1024;

        //Ceiling on the capacity *hint*, not on the body. Content-Length is whatever the
        //server claims: reserving the full declared length means one header saying 64 MB
        //costs 64 MB on the large object heap before the first byte arrives, whether or not
        //the body ever turns up. Past this the MemoryStream just grows as it fills.
        private const int MaxCapacityHint = 4 * 1024 * 1024;

        //Bytes searched for a NUL before deciding a body with no recognisable media type is
        //binary. Anything that is really binary trips over one long before this.
        private const int SniffBytes = 64 * 1024;

        //Tacked on where the body was cut, so a truncated body is recognisable wherever it
        //is shown - the session grid, the response pane, a synced copy. The Session.Truncated
        //column records the same fact structurally; this marker is for eyes, not code.
        private static readonly byte[] TruncatedMarker = Encoding.UTF8.GetBytes("\r\n[truncated - response exceeded the 64 MB cap]");

        /// <summary>
        /// What was actually received, as opposed to what the headers claimed.
        /// </summary>
        /// <param name="Truncated">The body was cut at the cap; the caller saves it on the session.</param>
        /// <param name="Bytes">
        /// Body bytes kept, excluding the truncation marker. Measured rather than read off
        /// Content-Length, which is absent on chunked and most HTTP/2 responses and is only a
        /// claim when it is there.
        /// </param>
        /// <param name="IsText">
        /// False when the body is not something a text pane can show. The session store keeps
        /// response bodies as text, so the caller stores a description instead of decoding
        /// bytes that would only come out as replacement characters.
        /// </param>
        /// <param name="MediaType">Content-Type without its parameters, or null when unstated.</param>
        public readonly record struct Buffered(bool Truncated, long Bytes, bool IsText, string MediaType);

        public static async Task<Buffered> BufferAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            HttpContent original = response.Content;

            await using Stream source = await original.ReadAsStreamAsync(cancellationToken);

            //A capacity hint around the declared length keeps the common case free of resizing
            //copies; the clamp keeps a lied-about Content-Length from reserving the whole cap.
            long declared = original.Headers.ContentLength ?? 0;
            int capacity = (int)Math.Clamp(declared + 256, 4096, MaxCapacityHint);
            var buffered = new MemoryStream(capacity);

            var chunk = new byte[81920];
            long total = 0;
            int read;
            bool truncated = false;

            while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
            {
                long room = MaxResponseBodyBytes - total;

                if (read > room)
                {
                    if (room > 0)
                    {
                        buffered.Write(chunk, 0, (int)room);
                        total += room;
                    }

                    buffered.Write(TruncatedMarker, 0, TruncatedMarker.Length);
                    truncated = true;
                    break;
                }

                buffered.Write(chunk, 0, read);
                total += read;
            }

            //Swap the network content for the buffered copy, headers preserved: later reads
            //must see exactly what was kept without a second pass over the network. The buffer
            //is handed over directly - at the cap, ToArray is another 64 MB on the large
            //object heap. ReadOnlyMemoryContent does not dispose or own the array, so the
            //MemoryStream wrapper is deliberately left to the GC rather than disposed.
            HttpContent replacement;
            ArraySegment<byte> body;

            if (buffered.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array is not null)
            {
                body = segment;
                replacement = new ReadOnlyMemoryContent(new ReadOnlyMemory<byte>(segment.Array, segment.Offset, segment.Count));
            }
            else
            {
                byte[] copy = buffered.ToArray();
                body = new ArraySegment<byte>(copy);
                replacement = new ByteArrayContent(copy);
            }

            string mediaType = original.Headers.ContentType?.MediaType;
            bool isText = IsTextual(mediaType, original.Headers.ContentType?.CharSet, body);

            foreach (var header in original.Headers) replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);

            response.Content = replacement;
            original.Dispose();

            return new Buffered(truncated, total, isText, mediaType);
        }

        /// <summary>
        /// Whether the body can be stored and shown as text. Deliberately generous: a body
        /// wrongly called binary is one the user can no longer read, while a binary one wrongly
        /// called text only reproduces the replacement characters this check exists to avoid.
        /// So only a media type that is definitely binary, or a NUL byte in the body itself,
        /// settles it - an unknown type carrying legacy-encoded text stays text.
        /// </summary>
        private static bool IsTextual(string mediaType, string charSet, ArraySegment<byte> body)
        {
            //The server said how to decode it, so it is text by its own account.
            if (!string.IsNullOrEmpty(charSet)) return true;

            if (!string.IsNullOrEmpty(mediaType))
            {
                if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;

                //application/problem+json, application/xhtml+xml, image/svg+xml and the rest
                //of the structured-suffix family.
                if (mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
                    || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)) return true;

                if (IsKnownTextualApplicationType(mediaType)) return true;

                //Whole families that are never text, so no sniffing is needed to be sure.
                if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    || mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                    || mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    || mediaType.StartsWith("font/", StringComparison.OrdinalIgnoreCase)) return false;
            }

            //Unknown or unstated type: let the bytes decide. Text does not contain NUL;
            //essentially every binary format has one within the first few kilobytes.
            int limit = Math.Min(body.Count, SniffBytes);

            for (int i = 0; i < limit; i++)
            {
                if (body.Array[body.Offset + i] == 0) return false;
            }

            return true;
        }

        private static bool IsKnownTextualApplicationType(string mediaType) => mediaType.ToLowerInvariant() switch
        {
            "application/json" or "application/xml" or "application/javascript"
                or "application/ecmascript" or "application/x-javascript"
                or "application/x-www-form-urlencoded" or "application/graphql"
                or "application/x-ndjson" or "application/ld+json"
                or "application/yaml" or "application/x-yaml" or "application/sql" => true,
            _ => false
        };
    }
}
