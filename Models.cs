using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiTester
{
    public class ServerCertificate
    {
        public string RequestUri { get; set; }
        public string ValidFrom { get; set; }
        public string ValidTo { get; set; }
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public bool IsValid { get; set; }
    }

    public class RequestTelemetry
    {
        public DateTime RequestStart { get; set; }
        public DateTime RequestStop { get; set; }

        public DateTime ResolutionStart { get; set; }
        public DateTime ResolutionStop { get; set; }

        public DateTime ConnectStart { get; set; }
        public DateTime ConnectStop { get; set; }

        public DateTime HandshakeStart { get; set; }
        public DateTime HandshakeStop { get; set; }

        public DateTime RequestHeadersStart { get; set; }
        public DateTime RequestHeadersStop { get; set; }

        public DateTime RequestContentStart { get; set; }
        public DateTime RequestContentStop { get; set; }

        public DateTime ResponseHeadersStart { get; set; }
        public DateTime ResponseHeadersStop { get; set; }

        public DateTime ResponseContentStart { get; set; }
        public DateTime ResponseContentStop { get; set; }

        public DateTime ConnectionEstablished { get; set; }

        public DateTime RequestLeftQueue { get; set; }

        public DateTime ConnectionClosed { get; set; }

        /// <summary>
        /// Milliseconds between two telemetry timestamps. Not every stage fires on every
        /// request - a pooled connection skips DNS, TCP and TLS entirely - so an unset
        /// endpoint reports 0 rather than a duration measured against a stale timestamp.
        /// </summary>
        public static double Duration(DateTime start, DateTime stop)
        {
            if (start == default || stop == default || stop < start) return 0;

            return (stop - start).TotalMilliseconds;
        }
    }

    public class Setting
    {       
        public int Id { get; set; }
        public string Endpoint { get; set; }
        public bool Selected { get; set; }
        public string ProfileName { get; set; }

        public int splitContainer6 { get; set; } = 90;
        public int splitContainer5 { get; set; } = 520;
        //public int splitContainer3 { get; set; } = 25;
        public int splitContainer2 { get; set; } = 520;
        public int splitContainer1 { get; set; } = 520;
        public int LocationX { get; set; } = 150;
        public int LocationY { get; set; } = 150;
        public int SizeWidth { get; set; } = 1536;
        public int SizeHeight { get; set; } = 1024;
        public string BlobSASToken { get; set; }
        public string BlobStorageAccount { get; set; }
        public string BlobContainer { get; set; }

        /// <summary>
        /// The storage account's access key, base64. Fallback authentication for networks whose
        /// proxy rewrites URLs and thereby invalidates a SAS - Shared Key travels in the
        /// Authorization header. It grants full control of the account, not just the container,
        /// so prefer a SAS anywhere it works.
        /// </summary>
        public string BlobAccountKey { get; set; }

        /// <summary>
        /// Passphrase the sync encrypts with. Empty means the container holds plain sessions.
        /// Every instance sharing a container needs the same one, or they cannot read what the
        /// others publish. See docs/blob-sync.md.
        /// </summary>
        public string BlobEncryptionKey { get; set; }

        /// <summary>
        /// When true, sessions sync to the configured Azure DevOps git repository instead of the
        /// blob container. Encryption is mandatory there - DevOpsSync refuses to run without a
        /// key - because a git repository is readable by its whole project team, not just
        /// whoever holds a token.
        /// </summary>
        public bool SyncWithDevOps { get; set; }

        /// <summary>Personal access token for the DevOps repository. Needs Code (read & write).</summary>
        public string DevOpsPat { get; set; }

        /// <summary>"host/projects/{teamProject}/_git/{repo}" - exactly as in the repo's URL.</summary>
        public string DevOpsRepo { get; set; }

        /// <summary>Branch the sync reads and commits to.</summary>
        public string DevOpsBranch { get; set; } = "main";

        //Column name is pinned so the rename does not orphan the existing stored value.
        [ColumnName("dataGridView1_col3_width")]
        public int DataGridViewCol3Width { get; set; } = 350;
    }

    public class Session
    {
        public int Id { get; set; }
        public string DateTime { get; set; }
        public string UriAbsoluteUri { get; set; }
        public string UriAbsolutePath { get; set; }
        public string UriQuery { get; set; }
        public string UriHost { get; set; }
        public string Method { get; set; }
        public string RequestHeaders { get; set; }
        public string RequestBody { get; set; }
        public string ResponseHeaders { get; set; }
        public byte[] ResponseBody { get; set; }
        public int ResponseStatusCode { get; set; }
        public string ResponseHttpVersion { get; set; }
        public string RequestHttpVersion { get; set; }
        public int ResponseLength { get; set; }
        public int ResponseTime { get; set; }
        public string Note { get; set; }
        public string Application { get; set; }
        public string Group { get; set; }
        public bool ServerCertIsValid { get; set; }
        public string ServerCertValidFrom { get; set; }
        public string ServerCertValidTo { get; set; }
        public string ServerCertSubject { get; set; }
        public string ServerCertIssuer { get; set; }
        public string ClientCertSubject { get; set; }
        public double DurationRequest { get; set; }
        public double DurationResolution { get; set; }
        public double DurationConnect { get; set; }
        public double DurationHandshake { get; set; }
        public double DurationRequestHeaders { get; set; }
        public double DurationRequestContent { get; set; }
        public double DurationResponseHeaders { get; set; }
        public double DurationResponseContent { get; set; }

        //--- Blob sync. See docs/blob-sync.md.

        /// <summary>
        /// Identifies the session across instances, and names its blob. The primary key cannot
        /// be used for either: every instance assigns its own.
        /// </summary>
        public string Uid { get; set; }

        /// <summary>
        /// ISO-8601 UTC of the last change to <see cref="Note"/> or <see cref="Group"/> - the
        /// only fields that can be edited, and so the only ones two instances can disagree on.
        /// The later timestamp wins.
        /// </summary>
        public string UpdatedUtc { get; set; }

        /// <summary>Has a change that the container has not accepted yet.</summary>
        public bool Dirty { get; set; }

        /// <summary>The row's blob exists in the container.</summary>
        public bool Uploaded { get; set; }

        /// <summary>
        /// Deleted locally, kept until the blob is gone too. Rows in this state are hidden
        /// from the grid.
        /// </summary>
        public bool Deleted { get; set; }
    }
}
