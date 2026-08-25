using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// The remote file store behind the Files tab. Two implementations, selected per profile by
    /// the same setting the sync follows (<see cref="Setting.SyncWithDevOps"/>):
    /// <see cref="AzureBlobFilesBackend"/> over the blob container, <see cref="DevOpsFilesBackend"/>
    /// over the Azure DevOps git repository.
    ///
    /// Everything here is plain transport: implementations take and return data, never touch a
    /// control and never log, so a transfer can also run on a worker thread - which is what a
    /// drag out to Explorer does.
    /// </summary>
    internal interface IFilesBackend
    {
        /// <summary>
        /// The files directly under <paramref name="prefix"/> plus the folders below it.
        /// </summary>
        /// <param name="prefix">Empty for the root, otherwise ends in a slash.</param>
        Task<List<BlobFile>> Browse(string prefix, CancellationToken ct);

        /// <summary>
        /// Every file under <paramref name="prefix"/>, however deep. Folders do not appear - only
        /// the files that make them up, which is what a recursive download, move or delete works
        /// on.
        /// </summary>
        Task<List<BlobFile>> BrowseTree(string prefix, CancellationToken ct);

        /// <param name="progress">Called with each chunk of bytes sent, never with a total.</param>
        Task Upload(string localPath, string remotePath, Action<long> progress, CancellationToken ct);

        /// <param name="progress">Called with each chunk of bytes written to disk, never with a total.</param>
        Task Download(string remotePath, string localPath, Action<long> progress, CancellationToken ct);

        /// <summary>
        /// Copies within the store. The blob service does it server-side; DevOps fetches the
        /// content and commits it under the new name.
        /// </summary>
        Task Copy(string sourcePath, string destinationPath, CancellationToken ct);

        /// <summary>
        /// Deletes a file - or, when <paramref name="path"/> ends in a slash, the marker that
        /// stands for a folder. A target that is already gone is not an error: two ways of
        /// removing the same folder can easily both reach the same file.
        /// </summary>
        Task Delete(string path, CancellationToken ct);

        /// <summary>
        /// Creates an empty folder, via the marker each backend keeps for the purpose - neither
        /// store has real directories. <paramref name="folderPath"/> ends in a slash.
        /// </summary>
        Task CreateFolder(string folderPath, CancellationToken ct);
    }

    /// <summary>
    /// One row of the file browser: either a stored file or a virtual directory.
    /// </summary>
    internal sealed class BlobFile
    {
        /// <summary>
        /// The full path in the store. A folder is the prefix that stands for it and always ends
        /// in a slash - neither backend has directories, only names that happen to contain one.
        /// </summary>
        public string BlobPath { get; init; }

        public bool IsFolder { get; init; }

        /// <summary>
        /// Size in bytes, or negative when the backend cannot say (a DevOps listing carries no
        /// sizes) - the size column and the totals leave it out then.
        /// </summary>
        public long Length { get; init; }

        /// <summary>Null when the backend does not report a modification time.</summary>
        public DateTime? Modified { get; init; }

        public string ContentType { get; init; }

        public string Name => LastSegment(IsFolder ? BlobPath.TrimEnd('/') : BlobPath);

        private static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            int slash = path.LastIndexOf('/');

            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
