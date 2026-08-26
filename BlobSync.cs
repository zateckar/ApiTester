using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    /// <summary>
    /// Keeps the session database of every instance pointed at the same container in step, one
    /// session at a time. See docs/blob-sync.md for the design and the reasoning behind it.
    /// </summary>
    public partial class Form1 : Form
    {
        private const string RowsFolder = "rows";
        private const string DelsFolder = "dels";
        private const string TicksFolder = "ticks";

        private const string MetaNote = "note";
        private const string MetaGroup = "grp";
        private const string MetaUpdated = "upd";
        private const string MetaSeq = "seq";

        //Bumped when the local bookkeeping changes shape, so the one-off migration below runs
        //again on databases that were prepared by an earlier version.
        private const string SyncSchemaVersion = "1";

        //A backlog is uploaded in rounds rather than in one go, so a first sync of a large
        //database does not monopolise the connection for minutes.
        private const int MaxPushPerRound = 100;

        private const int SyncDebounceMs = 2000;
        private const int SyncPollMs = 60000;
        private const int CloseFlushMs = 5000;

        //The WinForms timer, deliberately: its Tick runs on the UI thread, which is where every
        //part of the sync touches the grid and the shared connection.
        private System.Windows.Forms.Timer syncDebounceTimer;
        private System.Windows.Forms.Timer syncPollTimer;

        private bool syncRunning;
        private bool syncQueued;

        /// <summary>
        /// Set while something else owns the session database - an export copying the file, or
        /// an import writing rows into it. See <see cref="SuspendSync"/>.
        /// </summary>
        private bool syncSuspended;

        //Incremented when the active profile changes. A sync that started against the previous
        //profile's database checks this and stops rather than writing to the new one.
        private int syncGeneration;

        private bool syncPulledThisRun;
        private string syncInstanceId;

        //A sync that hits a failing request is shut off rather than retried every poll - with
        //a wrong credential or a missing branch it would otherwise log one error per dirty row,
        //every poll, forever. A local change deliberately does NOT re-arm it: a keystroke does
        //not fix a wrong PAT, and re-arming on every edit would run a fresh failing round on
        //each one. Reloading the settings or pressing the sync button clears this - see
        //Settings.cs and the verbose path in SyncNow.
        private bool syncBlocked;

        //Set when any store operation inside the round failed - even one is a connection or
        //configuration problem worth stopping for, not a single blob's bad luck. Checked by the
        //round's loops: the first failure ends the round, so a broken setup costs one request,
        //not one per dirty row.
        private bool syncFailedThisRound;

        /// <summary>
        /// Notes the outcome of one store operation.
        /// </summary>
        private void SyncOperationResult(bool ok)
        {
            if (!ok) syncFailedThisRound = true;
        }

        /// <summary>
        /// Anything in the round failed: block further attempts until the setup changes or the
        /// user asks. A flakey single-blob failure pays for this too - it takes a button press
        /// or a settings save to come back, which for a flake is a cheap enough price.
        /// </summary>
        private void SyncBlockIfAllFailed()
        {
            if (!syncBlocked && syncFailedThisRound)
            {
                syncBlocked = true;

                SyncLog("Sync disabled - at least one request failed. See the errors above. Fix the cause and press the sync button to retry; saving the settings also re-enables it.");
                SetSyncStatus("Sync disabled - errors", "The log lists the failing requests. Press the sync button, or save the settings, to re-enable.");
            }
        }

        //The derived key, and the passphrase and container it came from. Deriving costs a
        //deliberate fraction of a second, so it happens once rather than per blob.
        private byte[] syncKey;
        private string syncKeyDerivedFrom;

        //Counts blobs this round that could not be decrypted - almost always a key that does not
        //match what wrote them.
        private int syncUndecryptable;

        //A foreign row was listed but not pulled for a reason other than decryption (the read
        //failed or returned bytes that are not a session blob). Recording the tick then would
        //skip the row permanently, so the round is left un-recorded and retried next round.
        private bool pullIncomplete;

        /// <summary>
        /// A pull has changed the database but the grid has not caught up yet - it was busy
        /// being edited. The next round repaints it.
        /// </summary>
        private bool syncGridStale;

        private bool SyncConfigured => BlobConfigured(complain: false);

        /// <summary>
        /// Where this profile's blobs live inside the container. Derived from the database file
        /// name, so two profiles sharing a container do not share a namespace.
        /// </summary>
        private static string SyncPrefix()
        {
            string name = Path.GetFileNameWithoutExtension(_settings.Endpoint ?? string.Empty);
            var sanitized = new StringBuilder();

            //Deliberately ASCII only: a blob name may hold more, but nothing is gained by
            //having the namespace depend on how a path was encoded.
            foreach (char c in name)
            {
                bool safe = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                sanitized.Append(safe ? char.ToLowerInvariant(c) : '-');
            }

            string prefix = sanitized.ToString().Trim('-');

            return prefix.Length == 0 ? "sessions" : prefix;
        }

        private static string RowPath(string uid) => SyncPrefix() + "/" + RowsFolder + "/" + uid;
        private static string DelPath(string uid) => SyncPrefix() + "/" + DelsFolder + "/" + uid;
        private static string TickPath(string instance) => SyncPrefix() + "/" + TicksFolder + "/" + instance;

        private static string LastSegment(string blobName)
        {
            if (string.IsNullOrEmpty(blobName)) return string.Empty;

            int slash = blobName.LastIndexOf('/');

            return slash < 0 ? blobName : blobName.Substring(slash + 1);
        }

        private static string NewUid() => Guid.NewGuid().ToString("N");

        // ---------------------------------------------------------------- scheduling

        private void SetupSync()
        {
            //Changes are pushed a moment after the last one rather than per keystroke.
            syncDebounceTimer = new System.Windows.Forms.Timer { Interval = SyncDebounceMs };
            syncDebounceTimer.Tick += async (sender, e) =>
            {
                syncDebounceTimer.Stop();
                await SyncNow(verbose: false);
            };

            //And a slow poll, so an instance left open notices what the others have done.
            syncPollTimer = new System.Windows.Forms.Timer { Interval = SyncPollMs };
            syncPollTimer.Tick += async (sender, e) => await SyncNow(verbose: false);
            syncPollTimer.Start();
        }

        /// <summary>
        /// Asks for a sync shortly. Called after every change - collapsing a burst of them into
        /// one round is the whole point of the delay.
        /// </summary>
        private void RequestSync()
        {
            if (IsDisposed || Disposing) return;
            if (!SyncConfigured || syncDebounceTimer is null) return;

            //Deliberately not clearing syncBlocked here: a new change is not a fix for whatever
            //failed, and letting one re-arm the sync would spin a failing round on every edit.
            //The rows stay dirty and go out on the first round after the real re-arm.

            syncDebounceTimer.Stop();
            syncDebounceTimer.Start();
        }

        /// <summary>
        /// Keeps the sync off the session database until <see cref="ResumeSync"/>, so it can be
        /// copied or written to wholesale. A round already in flight is waited out rather than
        /// cut off - it is halfway through publishing sessions.
        /// </summary>
        /// <returns>False if a round is still running after the wait, in which case the sync was
        /// not suspended and the caller must not touch the database.</returns>
        private async Task<bool> SuspendSync()
        {
            //Set first, and only then wait: the poll and debounce timers tick on this thread, so
            //a modal file dialog would otherwise let a new round start while it is open.
            syncSuspended = true;

            for (int attempt = 0; syncRunning && attempt < 100; attempt++)
            {
                await Task.Delay(100);
            }

            if (syncRunning)
            {
                syncSuspended = false;
                return false;
            }

            return true;
        }

        private void ResumeSync()
        {
            syncSuspended = false;

            //Whatever happened while it was held off - an import's new rows, or a round that was
            //turned away - goes out now.
            RequestSync();
        }

        /// <summary>
        /// Pulls what the other instances have published, then publishes what this one has.
        /// Never throws: it runs off a timer, where an exception has nowhere to go.
        /// </summary>
        private async Task SyncNow(bool verbose)
        {
            if (!SyncConfigured)
            {
                if (verbose) BlobConfigured(complain: true);
                SetSyncStatus(string.Empty, null);
                return;
            }

            if (sessionsConn is null) return;

            //An export or import owns the database for the moment. ResumeSync starts a round.
            if (syncSuspended) return;

            activeSyncStore = SyncStoreForThisRun;

            //DevOps-hosted sync is only offered encrypted: the repository is readable by its
            //project team, and plaintext sessions there are a data leak, not a convenience.
            if (_settings.SyncWithDevOps && string.IsNullOrEmpty(_settings.BlobEncryptionKey))
            {
                if (verbose) MessageBox.Show("Sync to Azure DevOps requires the encryption key to be set - request bodies would otherwise land in a source repository in readable form.");
                SetSyncStatus("Sync: key required", "Set the encryption key in Settings to sync to Azure DevOps.");
                return;
            }

            //Everything failed in the last round and nothing changed the setup since - trying
            //again would produce the same 403s. Saving the settings reloads the profile, which
            //clears this; the sync button below is the manual escape hatch.
            if (syncBlocked)
            {
                if (verbose)
                {
                    //The explicit button stays an escape hatch.
                    syncBlocked = false;
                }
                else
                {
                    return;
                }
            }

            if (syncRunning)
            {
                syncQueued = true;
                return;
            }

            syncRunning = true;
            int generation = syncGeneration;

            try
            {
                SetSyncStatus("Sync...", null);

                await EnsureSyncSchema();
                if (generation != syncGeneration) return;

                //Deliberately not part of EnsureSyncSchema, which also runs while the window is
                //loading: deriving the key takes a moment and there is no need to hold up the grid.
                await CheckContainerIdentity();
                if (generation != syncGeneration) return;

                syncUndecryptable = 0;
                pullIncomplete = false;
                syncFailedThisRound = false;

                bool changed = await SyncPull(generation);
                if (generation != syncGeneration) return;

                //A failed pull means the view of the store cannot be trusted, and the rule is
                //stop-on-first-error: with anything failed, nothing more goes out this round.
                int pushed = syncFailedThisRound ? 0 : await SyncPush(generation);
                if (generation != syncGeneration) return;

                //Others watch this counter to decide whether it is worth listing the rows. Not
                //written after a failure - that would be one more request against a broken
                //setup, and the rows it advertises did not all land anyway.
                if (!syncFailedThisRound
                    && (pushed > 0 || string.Equals(await GetSyncState("tick-pending"), "1", StringComparison.Ordinal)))
                {
                    await WriteTick();
                }

                if (changed) syncGridStale = true;

                if (syncGridStale)
                {
                    //Rebuilding the grid clears its rows, which would throw away a note the user
                    //is in the middle of typing. The database is already up to date either way.
                    if (dataGridView1.IsCurrentCellInEditMode)
                    {
                        syncQueued = true;
                    }
                    else
                    {
                        await ReloadSessionRows();
                        await LoadGroups();

                        //Cleared last: a repaint that failed leaves the grid to be caught up on
                        //the next round rather than silently stale.
                        syncGridStale = false;
                    }
                }

                int pending = await sessionsConn.ScalarIntAsync("select count(*) from Session where Dirty = 1");

                //The re-encryption is done once nothing is waiting to go out.
                if (pending == 0) await SetSyncState("rekey", "0");

                //A poll that found nothing to do stays out of the log - it fires every minute
                //and the status label's "Synced <time>" already says the round ran. A round
                //that transferred something, or one the user asked for, gets a line.
                if (verbose || pushed > 0 || changed)
                {
                    SyncLog("Sync done. Received: " + (changed ? "yes" : "no")
                        + ", sent: " + pushed.ToString(CultureInfo.CurrentCulture)
                        + ", waiting: " + pending.ToString(CultureInfo.CurrentCulture));
                }

                if (syncUndecryptable > 0)
                {
                    //One line per round rather than one per session: with the wrong key every
                    //session in the container fails, and the point is to be noticed, not to bury
                    //everything else in the log.
                    string trouble = syncUndecryptable.ToString(CultureInfo.CurrentCulture)
                        + (syncKey is null
                            ? " sessions in the container are encrypted. Set the encryption key in Settings to read them."
                            : " sessions could not be decrypted. The encryption key does not match the one they were written with.");

                    SyncLog(trouble);
                    SetSyncStatus("Sync: key mismatch", trouble);
                }
                else
                {
                    SetSyncStatus(pending > 0
                            ? "Sync: " + pending.ToString(CultureInfo.CurrentCulture) + " waiting"
                            : "Synced " + DateTime.Now.ToString("t", CultureInfo.CurrentCulture),
                        null);
                }
            }
            catch (Exception ex)
            {
                //Including the unexpected: a broken sync must not take the app with it. An
                //exception that escaped an operation never flipped the failed flag, so set it
                //here - otherwise a configuration mistake would spin up a fresh round on every
                //poll and the circuit breaker would never see a failed operation to work from.
                syncFailedThisRound = true;

                SyncLog("Sync failed: " + ex.Message);
                SetSyncStatus("Sync failed", ex.Message);
            }
            finally
            {
                //Nothing got through at all: stop hammering the container. Done after the
                //queued-retry unblocking below - that retry is a follow-up change that already
                //happened, and this round's verdict updates it.
                syncRunning = false;

                SyncBlockIfAllFailed();

                if (syncQueued && !syncBlocked)
                {
                    syncQueued = false;
                    RequestSync();
                }
            }
        }

        /// <summary>
        /// Pushes what is outstanding while the window closes, but only briefly - anything left
        /// behind is still marked dirty and goes out on the next start.
        /// </summary>
        private async Task FlushSyncOnClose()
        {
            if (!SyncConfigured) return;

            syncPollTimer?.Stop();
            syncDebounceTimer?.Stop();

            await Task.WhenAny(SyncNow(verbose: false), Task.Delay(CloseFlushMs));
        }

        // ---------------------------------------------------------------- local bookkeeping

        /// <summary>
        /// Creates what the sync stores locally, and prepares sessions that predate it.
        /// </summary>
        private async Task EnsureSyncSchema()
        {
            //Adds Uid, UpdatedUtc, Dirty, Uploaded and Deleted to databases written before them.
            await sessionsConn.EnsureTableAsync<Session>();
            await sessionsConn.ExecuteAsync("create table if not exists SyncState (\"Key\" varchar primary key not null, \"Value\" varchar)");

            if (!string.Equals(await GetSyncState("schema"), SyncSchemaVersion, StringComparison.Ordinal))
            {
                //A column added by EnsureTableAsync starts out NULL; everything below reads
                //these as 0 or 1.
                await sessionsConn.ExecuteAsync("update Session set Dirty = 0 where Dirty is null");
                await sessionsConn.ExecuteAsync("update Session set Uploaded = 0 where Uploaded is null");
                await sessionsConn.ExecuteAsync("update Session set Deleted = 0 where Deleted is null");

                //Not "now": two instances would then disagree about whose copy of an old note is
                //newer purely by which of them was started first. From the epoch, any real edit
                //wins and two untouched copies stay equal.
                await sessionsConn.ExecuteAsync("update Session set UpdatedUtc = $epoch where UpdatedUtc is null or UpdatedUtc = ''", ("$epoch", EpochUtc));

                await BackfillUids();

                await SetSyncState("schema", SyncSchemaVersion);
            }
        }

        //Spelled as UTC explicitly. DateTime.MinValue carries no kind, and "o" then formats it
        //without the trailing Z - which parses back correctly here, but publishes a timestamp
        //that does not say what zone it is in.
        private static readonly string EpochUtc =
            DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

        /// <summary>
        /// Gives every session that predates the sync a uid derived from its own content, so two
        /// instances holding copies of the same session arrive at the same one and the container
        /// ends up with one blob rather than two.
        /// </summary>
        private async Task BackfillUids()
        {
            var rows = await sessionsConn.RawRowsAsync(
                "select Id, \"DateTime\", Method, UriAbsoluteUri, ResponseStatusCode, ResponseLength, ResponseTime"
                + " from Session where Uid is null or Uid = '' order by Id");

            if (rows.Count == 0) return;

            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (object[] row in rows)
            {
                string key = string.Join("\u001f",
                    SqliteStore.AsString(row[1]),
                    SqliteStore.AsString(row[2]),
                    SqliteStore.AsString(row[3]),
                    SqliteStore.AsInt(row[4]).ToString(CultureInfo.InvariantCulture),
                    SqliteStore.AsInt(row[5]).ToString(CultureInfo.InvariantCulture),
                    SqliteStore.AsInt(row[6]).ToString(CultureInfo.InvariantCulture));

                //The same request repeated inside one second is identical in all of those.
                //Numbering the repeats keeps their uids apart, and does it the same way on every
                //instance that holds the same history.
                int ordinal = ordinals.TryGetValue(key, out int used) ? used + 1 : 0;
                ordinals[key] = ordinal;

                await sessionsConn.ExecuteAsync("update Session set Uid = $uid where Id = $id",
                    ("$uid", DerivedUid(key + "\u001f" + ordinal.ToString(CultureInfo.InvariantCulture))),
                    ("$id", SqliteStore.AsInt(row[0])));
            }

            SyncLog("Prepared " + rows.Count.ToString(CultureInfo.CurrentCulture) + " existing sessions for sync.");
        }

        private static string DerivedUid(string key)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 16).ToLowerInvariant();

        /// <summary>
        /// The Uploaded flags only mean anything relative to one container. Pointing the profile
        /// somewhere else has to clear them, or sessions that were never published there would
        /// be treated as though they had been.
        /// </summary>
        private async Task CheckContainerIdentity()
        {
            if (!SyncConfigured) return;

            await EnsureKey();

            //The backend is part of the identity: switching between a container and a
            //repository must republish everything, or the new store would hold nothing.
            string identity = _settings.SyncWithDevOps
                ? "devops/" + (_settings.DevOpsRepo ?? string.Empty) + "/" + SyncPrefix()
                : _settings.BlobStorageAccount + "/" + _settings.BlobContainer + "/" + SyncPrefix();

            if (!string.Equals(await GetSyncState("container"), identity, StringComparison.OrdinalIgnoreCase))
            {
                await sessionsConn.ExecuteAsync("update Session set Uploaded = 0, Dirty = 1 where Deleted = 0");
                await sessionsConn.ExecuteAsync("delete from SyncState where \"Key\" like 'tick:%'");
                await SetSyncState("container", identity);

                syncPulledThisRun = false;

                SyncLog("Syncing with " + identity + ".");
            }

            //A different key means everything already up there is unreadable to us, and what we
            //publish would be unreadable to anyone still on the old one. Republish the lot.
            string fingerprint = SyncCrypto.Fingerprint(syncKey);

            //No recorded fingerprint means an unencrypted container: either this instance has never
            //synced, or it synced with a build that predates encryption. Both are "none", which is
            //also what no key fingerprints as - so no key before and no key now compares equal and
            //nothing happens.
            string previous = await GetSyncState("key-fp") ?? SyncCrypto.NoKeyFingerprint;

            if (string.Equals(previous, fingerprint, StringComparison.Ordinal)) return;

            await SetSyncState("key-fp", fingerprint);

            //Nothing published, so nothing to re-encrypt - just record which key is in use. This is
            //the ordinary case for an instance whose key was simply wrong until now: it published
            //nothing, and correcting the key is all it needed.
            if (await sessionsConn.ScalarIntAsync("select count(*) from Session where Uploaded = 1") == 0) return;

            await sessionsConn.ExecuteAsync("update Session set Uploaded = 0, Dirty = 1 where Deleted = 0");

            //The row blobs are still there under the old key, and a create that refuses to
            //overwrite would leave them. This one time, replace them.
            await SetSyncState("rekey", "1");

            SyncLog(syncKey is null
                ? "Encryption key removed - the container will be rewritten unencrypted."
                : "Encryption key changed - the container will be re-encrypted. Every instance needs the new key.");
        }

        /// <summary>
        /// Makes sure the encryption key is derived and current. Cheap after the first call; the
        /// derivation itself runs off the UI thread because it is deliberately slow.
        /// </summary>
        private async Task EnsureKey()
        {
            string passphrase = _settings.BlobEncryptionKey;

            //Includes the store: the same passphrase against a different container or repository
            //derives a different key, so one cannot be used to read the other. The blob leg keeps
            //the original account/container spelling - it is the salt existing containers were
            //already encrypted against.
            string account = _settings.SyncWithDevOps ? "devops|" + (_settings.DevOpsRepo ?? string.Empty) : _settings.BlobStorageAccount;
            string container = _settings.SyncWithDevOps ? string.Empty : _settings.BlobContainer;

            string derivedFrom = (passphrase ?? string.Empty) + "\n" + account + "\n" + container;

            if (string.Equals(syncKeyDerivedFrom, derivedFrom, StringComparison.Ordinal)) return;

            DiscardKey();

            if (!string.IsNullOrEmpty(passphrase))
            {
                syncKey = await Task.Run(() => SyncCrypto.DeriveKey(passphrase, account, container));
            }

            syncKeyDerivedFrom = derivedFrom;
        }

        private void DiscardKey()
        {
            if (syncKey is not null) CryptographicOperations.ZeroMemory(syncKey);

            syncKey = null;
            syncKeyDerivedFrom = null;
        }

        private Task<string> GetSyncState(string key)
            => sessionsConn.ScalarStringAsync("select \"Value\" from SyncState where \"Key\" = $key", ("$key", key));

        private Task SetSyncState(string key, string value)
            => sessionsConn.ExecuteAsync(
                "insert into SyncState (\"Key\", \"Value\") values ($key, $value) on conflict(\"Key\") do update set \"Value\" = $value",
                ("$key", key), ("$value", value));

        /// <summary>
        /// This installation's identity, minted on first use. Names its tick blob, and tells it
        /// which of the ticks in the container is its own.
        /// </summary>
        private async Task<string> InstanceId()
        {
            if (!string.IsNullOrEmpty(syncInstanceId)) return syncInstanceId;

            syncInstanceId = await GetSyncState("instance");

            if (string.IsNullOrEmpty(syncInstanceId))
            {
                syncInstanceId = NewUid();
                await SetSyncState("instance", syncInstanceId);
            }

            return syncInstanceId;
        }

        // ---------------------------------------------------------------- push

        private async Task<int> SyncPush(int generation)
        {
            var dirty = await sessionsConn.RawRowsAsync(
                "select Id, Uid, Uploaded, Deleted, UpdatedUtc, Note, \"Group\" from Session where Dirty = 1"
                + " order by Id limit " + MaxPushPerRound.ToString(CultureInfo.InvariantCulture));

            //More than one round's worth - come back for the rest.
            if (dirty.Count >= MaxPushPerRound) syncQueued = true;

            //Set only while a change of key is being rolled out, when the blobs that are already
            //there hold ciphertext nobody can read any more and have to be replaced.
            bool rekey = string.Equals(await GetSyncState("rekey"), "1", StringComparison.Ordinal);

            int pushed = 0;

            foreach (object[] row in dirty)
            {
                //IsDisposed/Disposing: the close-flush caps itself at CloseFlushMs and lets
                //the round run on while the window tears down - stop touching it instead.
                if (generation != syncGeneration || syncFailedThisRound || IsDisposed || Disposing) break;

                int id = SqliteStore.AsInt(row[0]);
                string uid = SqliteStore.AsString(row[1]);
                bool uploaded = SqliteStore.AsBool(row[2]);
                bool deleted = SqliteStore.AsBool(row[3]);
                string updated = SqliteStore.AsString(row[4]);

                if (string.IsNullOrEmpty(uid)) continue;

                //An empty metadata value is not sent at all, and a row whose timestamp never
                //arrived would then look to every reader like one that cannot be compared.
                if (string.IsNullOrEmpty(updated)) updated = EpochUtc;

                //The note and the group are the user's own text, so they are encrypted too. The
                //timestamp is not: the pull compares it to decide whether a session is worth
                //looking at, and that has to work straight off the listing.
                var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [MetaNote] = SyncCrypto.ProtectText(SqliteStore.AsString(row[5]), syncKey, uid + "|" + MetaNote, hex: _settings.SyncWithDevOps),
                    [MetaGroup] = SyncCrypto.ProtectText(SqliteStore.AsString(row[6]), syncKey, uid + "|" + MetaGroup, hex: _settings.SyncWithDevOps),
                    [MetaUpdated] = updated
                };

                if (deleted)
                {
                    if (await PushDelete(uid, updated) is not SyncStoreResult.Ok) continue;

                    await sessionsConn.ExecuteAsync("delete from Session where Id = $id", ("$id", id));
                    pushed++;
                    continue;
                }

                if (!uploaded)
                {
                    SyncStoreResult created = await PushRow(id, uid, metadata, rekey);

                    if (created == SyncStoreResult.Exists)
                    {
                        //Another instance derived the same uid for the same session. Its blob
                        //stands - but the note still has to be published, so the row stays dirty
                        //and takes the metadata path on the next round.
                        await sessionsConn.ExecuteAsync("update Session set Uploaded = 1 where Id = $id", ("$id", id));
                        syncQueued = true;
                        continue;
                    }

                    if (created != SyncStoreResult.Ok) continue;

                    await sessionsConn.ExecuteAsync("update Session set Uploaded = 1, Dirty = 0 where Id = $id", ("$id", id));
                    pushed++;
                    continue;
                }

                SyncStoreResult result = await activeSyncStore.PutMetadata(RowPath(uid), metadata);

                if (result == SyncStoreResult.Missing)
                {
                    //Deleted elsewhere, but edited here. Put the session back rather than dropping
                    //the edit: the next round uploads it in full.
                    await sessionsConn.ExecuteAsync("update Session set Uploaded = 0 where Id = $id", ("$id", id));
                    continue;
                }

                if (result != SyncStoreResult.Ok) continue;

                await sessionsConn.ExecuteAsync("update Session set Dirty = 0 where Id = $id", ("$id", id));
                pushed++;
            }

            return pushed;
        }

        private async Task<SyncStoreResult> PushRow(int id, string uid, IReadOnlyDictionary<string, string> metadata, bool overwrite)
        {
            Session session = await sessionsConn.FindAsync<Session>(id);
            if (session is null) return SyncStoreResult.Failed;

            byte[] content = SyncRow.Write(session);

            //Bound to this uid, so a ciphertext copied to another blob's name will not open.
            if (syncKey is not null) content = SyncCrypto.Protect(content, syncKey, uid);

            //A row blob is written once and never rewritten, so refusing to overwrite costs
            //nothing and makes a repeated push harmless. Rolling out a new key is the exception.
            return await activeSyncStore.Put(RowPath(uid), content, metadata, onlyIfMissing: !overwrite);
        }

        /// <summary>
        /// Removes the session's blob and leaves a tombstone in its place. The tombstone is what
        /// tells the other instances to delete their copy - a row simply missing from the
        /// container is never read as deleted, because a container that was recreated, or a
        /// listing that stopped early, looks exactly the same.
        /// </summary>
        private async Task<SyncStoreResult> PushDelete(string uid, string updated)
        {
            SyncStoreResult removed = await activeSyncStore.Delete(RowPath(uid));
            if (removed != SyncStoreResult.Ok) return removed;

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [MetaUpdated] = updated };

            SyncStoreResult tombstone = await activeSyncStore.Put(DelPath(uid), Array.Empty<byte>(), metadata, onlyIfMissing: false);

            return tombstone == SyncStoreResult.Exists ? SyncStoreResult.Ok : tombstone;
        }

        private async Task WriteTick()
        {
            string instance = await InstanceId();

            if (!int.TryParse(await GetSyncState("push-seq"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seq)) seq = 0;
            seq++;

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetaSeq] = seq.ToString(CultureInfo.InvariantCulture)
            };

            if (await activeSyncStore.Put(TickPath(instance), Array.Empty<byte>(), metadata, onlyIfMissing: false) == SyncStoreResult.Ok)
            {
                await SetSyncState("push-seq", seq.ToString(CultureInfo.InvariantCulture));
                await SetSyncState("tick-pending", "0");
            }
            else
            {
                //Until this lands, the others have no reason to look at the rows we just wrote.
                await SetSyncState("tick-pending", "1");
            }
        }

        // ---------------------------------------------------------------- pull

        /// <summary>
        /// One local session, as the diff needs to see it. Loading whole sessions here would
        /// mean reading every stored response body just to compare a timestamp.
        /// </summary>
        private sealed class LocalRow
        {
            public int Id { get; init; }
            public string UpdatedUtc { get; init; }
            public bool Uploaded { get; init; }
            public bool Dirty { get; init; }
            public bool Deleted { get; init; }
        }

        /// <returns>Whether anything in the local database changed.</returns>
        private async Task<bool> SyncPull(int generation)
        {
            string prefix = SyncPrefix();
            string self = await InstanceId();

            var ticks = await activeSyncStore.List(prefix + "/" + TicksFolder + "/", includeMetadata: true);
            if (ticks is null)
            {
                SyncLog("Pull: tick listing failed.");
                return false;
            }

            //One full pass per run whatever the ticks say: another instance may have pushed and
            //then failed to write its tick, and there would be no other way to notice.
            bool remoteChanged = !syncPulledThisRun;
            var seen = new List<(string Instance, string Seq)>();

            foreach (SyncEntry tick in ticks)
            {
                string instance = LastSegment(tick.Name);
                if (string.Equals(instance, self, StringComparison.OrdinalIgnoreCase)) continue;

                string seq = tick.Meta(MetaSeq) ?? string.Empty;
                seen.Add((instance, seq));

                if (!string.Equals(await GetSyncState("tick:" + instance), seq, StringComparison.Ordinal)) remoteChanged = true;
            }

            SyncLog("Pull: ticks=" + ticks.Count + " seen=" + seen.Count + " remoteChanged=" + remoteChanged);

            //Nothing has been pushed since the last look. This is the ordinary case, and it costs
            //one small listing rather than a walk over every session in the container.
            if (!remoteChanged) return false;

            var remote = await activeSyncStore.List(prefix + "/" + RowsFolder + "/", includeMetadata: true);
            if (remote is null)
            {
                SyncLog("Pull: rows listing failed.");
                return false;
            }

            var tombstones = await activeSyncStore.List(prefix + "/" + DelsFolder + "/", includeMetadata: false);
            if (tombstones is null)
            {
                SyncLog("Pull: dels listing failed.");
                return false;
            }

            SyncLog("Pull: " + remote.Count + " remote rows, " + tombstones.Count + " tombstones.");

            var local = await LocalUidIndex();
            bool changed = false;

            foreach (SyncEntry entry in remote)
            {
                if (generation != syncGeneration || syncFailedThisRound || IsDisposed || Disposing) return changed;

                string uid = LastSegment(entry.Name);
                if (uid.Length == 0) continue;

                if (!local.TryGetValue(uid, out LocalRow row))
                {
                    if (await PullRow(entry, uid)) changed = true;
                    continue;
                }

                //Our own delete is still on its way out; it must not be undone here.
                if (row.Deleted) continue;

                string updated = entry.Meta(MetaUpdated);

                DateTime theirs = SyncRow.ParseUtc(updated);
                DateTime ours = SyncRow.ParseUtc(row.UpdatedUtc);

                if (!string.IsNullOrEmpty(updated) && theirs > ours)
                {
                    if (!TryReadMeta(entry, uid, out string note, out string group))
                    {
                        //Written with a key we do not have. Leaving our copy alone is the only
                        //safe move - overwriting it with an unreadable value would lose it.
                        syncUndecryptable++;
                        continue;
                    }

                    //Later edit wins. Note and group are the only fields that can differ.
                    await sessionsConn.ExecuteAsync(
                        "update Session set Note = $note, \"Group\" = $grp, UpdatedUtc = $upd, Dirty = 0, Uploaded = 1 where Id = $id",
                        ("$note", note),
                        ("$grp", group),
                        ("$upd", updated),
                        ("$id", row.Id));

                    changed = true;
                }
                else if (!string.IsNullOrEmpty(updated) && theirs < ours)
                {
                    //Ours is the later edit, and the container is holding an older note. That
                    //happens on a first sync, where another instance published its copy of a
                    //session we had already edited. Marking it dirty publishes ours in the push
                    //that follows, which is what makes the two converge rather than sit and
                    //disagree - the push does not compare timestamps, so nothing else would.
                    await sessionsConn.ExecuteAsync("update Session set Dirty = 1, Uploaded = 1 where Id = $id", ("$id", row.Id));
                }
                else if (!row.Uploaded && !row.Dirty)
                {
                    //The blob is already there under a uid we derived independently.
                    await sessionsConn.ExecuteAsync("update Session set Uploaded = 1 where Id = $id", ("$id", row.Id));
                }
            }

            foreach (SyncEntry entry in tombstones)
            {
                if (generation != syncGeneration || syncFailedThisRound || IsDisposed || Disposing) return changed;

                if (!local.TryGetValue(LastSegment(entry.Name), out LocalRow row)) continue;

                //Deleting wins over an edit made elsewhere in the meantime.
                await sessionsConn.ExecuteAsync("delete from Session where Id = $id", ("$id", row.Id));
                changed = true;
            }

            //Remembering the counters is what makes the next round cheap - but only when this one
            //actually read everything. After a decryption failure the round is left un-recorded so
            //that correcting the key is enough to pick the sessions up, with no further change
            //needed anywhere else.
            if (syncUndecryptable == 0 && !pullIncomplete)
            {
                foreach (var (instance, seq) in seen) await SetSyncState("tick:" + instance, seq);

                syncPulledThisRun = true;
            }

            return changed;
        }

        private async Task<Dictionary<string, LocalRow>> LocalUidIndex()
        {
            var index = new Dictionary<string, LocalRow>(StringComparer.OrdinalIgnoreCase);

            foreach (object[] row in await sessionsConn.RawRowsAsync(
                "select Uid, Id, UpdatedUtc, Uploaded, Dirty, Deleted from Session where Uid is not null and Uid <> ''"))
            {
                index[SqliteStore.AsString(row[0])] = new LocalRow
                {
                    Id = SqliteStore.AsInt(row[1]),
                    UpdatedUtc = SqliteStore.AsString(row[2]),
                    Uploaded = SqliteStore.AsBool(row[3]),
                    Dirty = SqliteStore.AsBool(row[4]),
                    Deleted = SqliteStore.AsBool(row[5])
                };
            }

            return index;
        }

        /// <summary>
        /// Reads a session's editable fields out of the blob's metadata.
        /// </summary>
        /// <returns>False when they are encrypted with a key this instance does not have.</returns>
        private bool TryReadMeta(SyncEntry entry, string uid, out string note, out string group)
        {
            group = string.Empty;

            return SyncCrypto.TryUnprotectText(entry.Meta(MetaNote), syncKey, uid + "|" + MetaNote, out note, hex: _settings.SyncWithDevOps)
                && SyncCrypto.TryUnprotectText(entry.Meta(MetaGroup), syncKey, uid + "|" + MetaGroup, out group, hex: _settings.SyncWithDevOps);
        }

        private async Task<bool> PullRow(SyncEntry entry, string uid)
        {
            byte[] content = await activeSyncStore.Get(entry.Name);
            if (content is null)
            {
                //The row is in the listing but its body did not come back. Swallowing this
                //silently once recorded the tick anyway, and the gate above then skipped the
                //row on every later round - foreign sessions ignored forever, with no log line.
                pullIncomplete = true;
                SyncLog("Could not pull " + entry.Name + ": no content; retried on the next round.");
                return false;
            }

            if (SyncCrypto.LooksEncrypted(content))
            {
                if (!SyncCrypto.TryUnprotect(content, syncKey, uid, out byte[] plaintext))
                {
                    //Encrypted with a key we do not have, or altered in the container. Either way
                    //there is nothing to insert.
                    syncUndecryptable++;
                    return false;
                }

                content = plaintext;
            }

            Session session = SyncRow.Read(content);

            if (session is null)
            {
                pullIncomplete = true;
                SyncLog("Ignored " + entry.Name + ": not a session blob.");
                return false;
            }

            //Keys are local; this database assigns its own.
            session.Id = 0;
            session.Uid = uid;

            string updated = entry.Meta(MetaUpdated);

            if (!string.IsNullOrEmpty(updated))
            {
                if (!TryReadMeta(entry, uid, out string note, out string group))
                {
                    syncUndecryptable++;
                    return false;
                }

                //Metadata holds the current note and group. The header only holds what they were
                //when the session was first published.
                session.Note = note;
                session.Group = group;
                session.UpdatedUtc = updated;
            }

            session.Dirty = false;
            session.Uploaded = true;
            session.Deleted = false;

            await sessionsConn.InsertAsync(session);

            return true;
        }

        // ---------------------------------------------------------------- reporting

        //Reporting must never be the thing that breaks the sync, and a round can still be in
        //flight while the window closes - the controls it writes to are gone by then.
        /// <summary>
        /// A line into the sync log. Stores live outside Form1 and reach the log through this
        /// and <see cref="SyncOpResult"/> only.
        /// </summary>
        internal void StoreLog(string message) => SyncLog(message);

        private void SyncLog(string message)
        {
            try
            {
                //Mirror to a plain file next to the session database so sync behaviour can be
                //inspected without the window. Best-effort: logging must never break the sync.
                string path = Path.Combine(AppContext.BaseDirectory, "sync.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture) + message + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

            if (IsDisposed || Disposing) return;

            try
            {
                //Bounded: the sync runs for as long as the app does.
                while (listBox_blob.Items.Count > 200) listBox_blob.Items.RemoveAt(listBox_blob.Items.Count - 1);

                listBox_blob.Items.Insert(0, DateTime.Now.ToString("T", CultureInfo.CurrentCulture) + "  " + message);
            }
            catch (ObjectDisposedException) { }
        }

        private void SetSyncStatus(string text, string tooltip)
        {
            if (IsDisposed || Disposing) return;

            try
            {
                toolStripStatusLabel_sync.Text = text;
                toolStripStatusLabel_sync.ToolTipText = tooltip ?? string.Empty;
            }
            catch (ObjectDisposedException) { }
        }
    }
}
