using System;
using System.Security.Cryptography;
using System.Text;

namespace ApiTester
{
    /// <summary>
    /// Encrypts what the sync puts in the container, so the sessions exist in the clear only on
    /// the machines that hold the key. AES-256-GCM, which is authenticated: a blob that has been
    /// tampered with fails to decrypt rather than being quietly trusted.
    ///
    /// This protects the container. It does not protect the local database - sessions are stored
    /// on disk exactly as unencrypted as they were before, and so is the key itself. What it buys
    /// is that a leaked SAS token, or anyone who can read the storage account, gets ciphertext
    /// instead of every request header this app has ever sent.
    /// </summary>
    internal static class SyncCrypto
    {
        //Envelope: magic | nonce | tag | ciphertext. The magic is what makes an encrypted blob
        //recognisable, so a container written before the key was set stays readable.
        private static readonly byte[] Magic = { (byte)'A', (byte)'T', (byte)'E', 1 };

        private const int NonceSize = 12;   //96 bits, the size GCM is specified for
        private const int TagSize = 16;
        private const int Overhead = 4 + NonceSize + TagSize;

        private const int KeySize = 32;     //AES-256

        //OWASP's floor for PBKDF2-SHA256 is 600,000, which measured 59 ms on a machine with SHA
        //hardware acceleration - and an attacker guessing passphrases against a stolen container
        //has the same acceleration, on better hardware. This is five times the floor, about 300 ms
        //here, paid once per key and off the UI thread, so nothing waits on it.
        //
        //PBKDF2 is not memory hard: it parallelises well on a GPU. Iterations buy margin, not
        //safety, and the passphrase is still what the container's secrecy rests on. Use a long
        //random one - see docs/blob-sync.md.
        private const int Iterations = 3_000_000;

        /// <summary>
        /// Turns the passphrase from the settings tab into a key. Every instance derives the same
        /// one from the same passphrase and container - they have no other way to agree. The salt
        /// is what stops two different containers from sharing a key.
        /// </summary>
        public static byte[] DeriveKey(string passphrase, string account, string container)
        {
            if (string.IsNullOrEmpty(passphrase)) return null;

            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes(
                "ApiTester.BlobSync.v1|" + (account ?? string.Empty) + "|" + (container ?? string.Empty)));

            return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        }

        /// <summary>
        /// What no key fingerprints as, and therefore what an unencrypted container looks like.
        /// </summary>
        public const string NoKeyFingerprint = "none";

        /// <summary>
        /// Identifies a key without disclosing it, so a change of key can be noticed and the
        /// container re-encrypted. Only ever stored locally.
        /// </summary>
        public static string Fingerprint(byte[] key)
            => key is null ? NoKeyFingerprint : Convert.ToHexString(SHA256.HashData(key), 0, 8).ToLowerInvariant();

        public static bool LooksEncrypted(byte[] blob)
        {
            if (blob is null || blob.Length < Overhead) return false;

            for (int i = 0; i < Magic.Length; i++)
            {
                if (blob[i] != Magic[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// Encrypts, binding the result to where it is about to be stored: a ciphertext moved to
        /// another blob's name no longer authenticates, so blobs cannot be swapped around.
        /// </summary>
        public static byte[] Protect(byte[] plaintext, byte[] key, string context)
        {
            plaintext ??= Array.Empty<byte>();

            var output = new byte[Overhead + plaintext.Length];
            Magic.CopyTo(output, 0);

            //A nonce must never repeat for a given key, and 96 random bits per message is the
            //standard way to get that without keeping a counter anywhere.
            var nonce = new Span<byte>(output, 4, NonceSize);
            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(key, TagSize);

            aes.Encrypt(nonce,
                        plaintext,
                        new Span<byte>(output, Overhead, plaintext.Length),
                        new Span<byte>(output, 4 + NonceSize, TagSize),
                        Context(context));

            return output;
        }

        /// <summary>
        /// Decrypts. Returns false for the wrong key, a corrupted blob, or one that was written
        /// somewhere else - never a guess at the plaintext.
        /// </summary>
        public static bool TryUnprotect(byte[] blob, byte[] key, string context, out byte[] plaintext)
        {
            plaintext = null;

            if (key is null || !LooksEncrypted(blob)) return false;

            var output = new byte[blob.Length - Overhead];

            try
            {
                using var aes = new AesGcm(key, TagSize);

                aes.Decrypt(new ReadOnlySpan<byte>(blob, 4, NonceSize),
                            new ReadOnlySpan<byte>(blob, Overhead, output.Length),
                            new ReadOnlySpan<byte>(blob, 4 + NonceSize, TagSize),
                            output,
                            Context(context));
            }
            catch (CryptographicException)
            {
                //Wrong key, altered bytes, or a blob written under a different name.
                return false;
            }

            plaintext = output;
            return true;
        }

        private static byte[] Context(string context)
            => Encoding.UTF8.GetBytes(context ?? string.Empty);

        /// <summary>
        /// Prepares a string for transport in a metadata field: base64 for blob headers (they
        /// must be ASCII and single-line), hex where the field lives inside a JSON file. An
        /// empty value returns null, which means "leave the key out".
        /// </summary>
        public static string ProtectText(string value, byte[] key, string context, bool hex = false)
        {
            if (string.IsNullOrEmpty(value)) return null;

            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            byte[] raw = key is null ? utf8 : Protect(utf8, key, context);

            return hex ? Convert.ToHexString(raw).ToLowerInvariant() : Convert.ToBase64String(raw);
        }

        /// <summary>
        /// Reads a metadata string back. Accepts plaintext whether or not a key is set, so a
        /// container that predates encryption keeps working; fails only when the value is
        /// encrypted and cannot be opened.
        /// </summary>
        public static bool TryUnprotectText(string stored, byte[] key, string context, out string value, bool hex = false)
        {
            value = string.Empty;

            if (string.IsNullOrEmpty(stored)) return true;

            byte[] raw;

            try
            {
                raw = hex ? Convert.FromHexString(stored) : Convert.FromBase64String(stored);
            }
            catch (FormatException)
            {
                //Written by something that did not encode it - take it at face value.
                value = stored;
                return true;
            }

            if (!LooksEncrypted(raw))
            {
                value = Encoding.UTF8.GetString(raw);
                return true;
            }

            if (!TryUnprotect(raw, key, context, out byte[] plaintext)) return false;

            value = Encoding.UTF8.GetString(plaintext);
            return true;
        }
    }
}
