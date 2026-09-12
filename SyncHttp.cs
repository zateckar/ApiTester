using System;
using System.Net;
using System.Net.Http;

namespace ApiTester
{
    /// <summary>
    /// The one place HttpClient handler policy lives for everything but the interactive
    /// request path (SendRequest needs a fresh handler per request - the per-request telemetry
    /// listener depends on it). Sync and file backends were drifting into four hand-copied
    /// blocks of the same settings; a fifth client added anywhere else must not silently miss
    /// decompression or pooled-connection lifetime.
    /// </summary>
    internal static class SyncHttp
    {
        /// <summary>
        /// Decompression on: transparently decompressed content is signed over its compressed
        /// form (Azure metadata signatures) or stored compressed and undisplayable (DevOps
        /// blobs), so the handler, not the caller, owns decompression. PooledConnectionLifetime:
        /// through a proxy or across DNS changes a stale pooled connection is a request that
        /// hangs exactly once, then silently reworks on retry.
        /// </summary>
        public static HttpClient CreateClient(TimeSpan? timeout = null)
            => new(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            {
                Timeout = timeout ?? System.Threading.Timeout.InfiniteTimeSpan
            };
    }
}
