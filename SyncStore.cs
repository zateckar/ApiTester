using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// How one write against the sync store turned out. Neither Missing nor Exists is a failure:
    /// they say what the store already holds, which the sync reacts to rather than retries.
    /// </summary>
    internal enum SyncStoreResult
    {
        Ok,

        /// <summary>The object was already there and was left alone.</summary>
        Exists,

        /// <summary>There is no such object - another instance has deleted the session.</summary>
        Missing,

        /// <summary>Another commit landed first (DevOps only). Not a failure: the caller
        /// leaves the row dirty and the next round retries it, while the round itself is
        /// healthy and keeps going.</summary>
        Conflict,

        Failed
    }

    /// <summary>
    /// One entry of a store listing: a name, plus the key/value pairs a reader can compare
    /// without fetching the content - the blob's metadata, or the decode of the JSON that
    /// stands in for it under DevOps.
    /// </summary>
    internal sealed class SyncEntry
    {
        public string Name { get; init; }
        public Dictionary<string, string> Metadata { get; init; }

        public string Meta(string key)
            => Metadata is not null && Metadata.TryGetValue(key, out string value) ? value : null;
    }

    /// <summary>
    /// Where the session sync stores its objects: the Azure blob container, or the Azure DevOps
    /// git repository. Every method is safe to call off the UI thread and reports through
    /// <see cref="Form1.SyncOperationResult"/> and the sync log.
    /// </summary>
    internal interface ISyncStore
    {
        /// <summary>Failure detail include the target object.</summary>
        Task<SyncStoreResult> Put(string path, byte[] content, IReadOnlyDictionary<string, string> metadata, bool onlyIfMissing);

        Task<SyncStoreResult> PutMetadata(string path, IReadOnlyDictionary<string, string> metadata);

        Task<SyncStoreResult> Delete(string path);

        /// <returns>Content, or null when it could not be read.</returns>
        Task<byte[]> Get(string path);

        /// <returns>Every entry under the prefix, or null when any part failed - a partial
        /// listing must never be mistaken for a complete one.</returns>
        Task<List<SyncEntry>> List(string prefix, bool includeMetadata);
    }
}
