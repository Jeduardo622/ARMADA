using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Armada.Client.Core
{
    public interface IGuestCredentialStore
    {
        bool IsSupported { get; }
        string Load();
        void Save(string credential);
    }

    public static class GuestCredentialStore
    {
        public static string OriginNamespace(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo)
                || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
                throw new ArgumentException("Guest credentials require HTTPS or loopback development.", nameof(baseUrl));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant())))
                .Replace("-", "").ToLowerInvariant();
        }

        public static IGuestCredentialStore Create(string baseUrl)
        {
            // Null is supported only for existing test fixtures that inject
            // private state. It never permits registration or disk access.
            if (string.IsNullOrWhiteSpace(baseUrl)) return new UnsupportedStore();
            var origin = OriginNamespace(baseUrl);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return new ProtectedFileStore(Application.persistentDataPath, origin, new WindowsProtection(origin));
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new ProtectedFileStore(Application.persistentDataPath, origin, new AndroidProtection(origin));
#else
            return new UnsupportedStore();
#endif
        }

        private sealed class UnsupportedStore : IGuestCredentialStore
        {
            public bool IsSupported => false;
            public string Load() => throw new PlatformNotSupportedException();
            public void Save(string credential) => throw new PlatformNotSupportedException();
        }

        public interface IProtection
        {
            byte[] Protect(byte[] plaintext);
            byte[] Unprotect(byte[] ciphertext);
        }

        // Only ciphertext reaches the filesystem. Atomic replacement preserves
        // the previous credential if encryption or writing fails.
        public sealed class ProtectedFileStore : IGuestCredentialStore
        {
            private readonly string _path;
            private readonly IProtection _protection;
            public bool IsSupported => true;
            public ProtectedFileStore(string directory, string originNamespace, IProtection protection)
            {
                if (originNamespace == null || !System.Text.RegularExpressions.Regex.IsMatch(originNamespace, "^[a-f0-9]{64}$"))
                    throw new ArgumentException("Invalid credential namespace.", nameof(originNamespace));
                _path = Path.Combine(directory, "guest-" + originNamespace + ".credential");
                _protection = protection ?? throw new ArgumentNullException(nameof(protection));
            }
            public string Load()
            {
                byte[] ciphertext;
                try
                {
                    if (new FileInfo(_path).Length > 65536) throw new CryptographicException("Invalid guest storage size.");
                    ciphertext = File.ReadAllBytes(_path);
                }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
                var plaintext = _protection.Unprotect(ciphertext);
                try { return Encoding.UTF8.GetString(plaintext); }
                finally { Array.Clear(plaintext, 0, plaintext.Length); }
            }
            public void Save(string credential)
            {
                var plaintext = Encoding.UTF8.GetBytes(credential);
                byte[] ciphertext;
                try { ciphertext = _protection.Protect(plaintext); }
                finally { Array.Clear(plaintext, 0, plaintext.Length); }
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(ciphertext, 0, ciphertext.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(_path)) File.Replace(temporary, _path, null);
                    else File.Move(temporary, _path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Current-user DPAPI; deliberately never CRYPTPROTECT_LOCAL_MACHINE.
        // https://learn.microsoft.com/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata
        public sealed class WindowsProtection : IProtection
        {
            private readonly byte[] _entropy;
            public WindowsProtection(string originNamespace) { _entropy = Encoding.UTF8.GetBytes(originNamespace); }
            [StructLayout(LayoutKind.Sequential)]
            private struct Blob { public int Length; public IntPtr Data; }
            [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CryptProtectData(ref Blob input, string description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
            [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
            [DllImport("kernel32.dll")]
            private static extern IntPtr LocalFree(IntPtr memory);
            public byte[] Protect(byte[] plaintext) => Transform(plaintext, true);
            public byte[] Unprotect(byte[] ciphertext) => Transform(ciphertext, false);
            private byte[] Transform(byte[] bytes, bool encrypt)
            {
                var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
                var entropy = new Blob { Length = _entropy.Length, Data = Marshal.AllocHGlobal(_entropy.Length) };
                var output = new Blob();
                try
                {
                    Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                    Marshal.Copy(_entropy, 0, entropy.Data, _entropy.Length);
                    var success = encrypt
                        ? CryptProtectData(ref input, null, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output)
                        : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output);
                    if (!success) throw new CryptographicException("Protected credential storage failed.");
                    var result = new byte[output.Length];
                    Marshal.Copy(output.Data, result, 0, result.Length);
                    return result;
                }
                finally
                {
                    for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
                    Marshal.FreeHGlobal(input.Data);
                    Marshal.FreeHGlobal(entropy.Data);
                    if (output.Data != IntPtr.Zero)
                    {
                        for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                        LocalFree(output.Data);
                    }
                }
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        // Non-exportable AndroidKeyStore AES key; a fresh GCM IV is generated
        // by Cipher for every save. API 23+, fail closed on older devices.
        // https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec
        private sealed class AndroidProtection : IProtection
        {
            private readonly string _alias;
            public AndroidProtection(string origin) { _alias = "armada.guest." + origin; }
            private AndroidJavaObject Key(bool create)
            {
                using var keyStoreClass = new AndroidJavaClass("java.security.KeyStore");
                using var keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", "AndroidKeyStore");
                keyStore.Call("load", (object)null, (object)null);
                if (!keyStore.Call<bool>("containsAlias", _alias))
                {
                    if (!create) throw new CryptographicException("Guest storage key unavailable.");
                    using var generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator");
                    using var generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "AES", "AndroidKeyStore");
                    using var builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", _alias, 3);
                    using var modes = builder.Call<AndroidJavaObject>("setBlockModes", (object)new[] { "GCM" });
                    using var padding = builder.Call<AndroidJavaObject>("setEncryptionPaddings", (object)new[] { "NoPadding" });
                    using var spec = builder.Call<AndroidJavaObject>("build");
                    generator.Call("init", spec);
                    using var generated = generator.Call<AndroidJavaObject>("generateKey");
                }
                return keyStore.Call<AndroidJavaObject>("getKey", _alias, (object)null);
            }
            public byte[] Protect(byte[] plaintext)
            {
                using var key = Key(true);
                using var cipherClass = new AndroidJavaClass("javax.crypto.Cipher");
                using var cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding");
                cipher.Call("init", 1, key);
                var iv = Unsigned(cipher.Call<sbyte[]>("getIV"));
                var encrypted = Unsigned(cipher.Call<sbyte[]>("doFinal", (object)Signed(plaintext)));
                if (iv.Length != 12) throw new CryptographicException("Unexpected GCM IV.");
                var result = new byte[1 + iv.Length + encrypted.Length];
                result[0] = 1;
                Buffer.BlockCopy(iv, 0, result, 1, iv.Length);
                Buffer.BlockCopy(encrypted, 0, result, 13, encrypted.Length);
                return result;
            }
            public byte[] Unprotect(byte[] ciphertext)
            {
                if (ciphertext.Length < 29 || ciphertext[0] != 1) throw new CryptographicException("Invalid guest storage.");
                var iv = new byte[12];
                var encrypted = new byte[ciphertext.Length - 13];
                Buffer.BlockCopy(ciphertext, 1, iv, 0, 12);
                Buffer.BlockCopy(ciphertext, 13, encrypted, 0, encrypted.Length);
                using var key = Key(false);
                using var cipherClass = new AndroidJavaClass("javax.crypto.Cipher");
                using var cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding");
                using var spec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", 128, Signed(iv));
                cipher.Call("init", 2, key, spec);
                return Unsigned(cipher.Call<sbyte[]>("doFinal", (object)Signed(encrypted)));
            }
            private static sbyte[] Signed(byte[] value) { var result = new sbyte[value.Length]; Buffer.BlockCopy(value, 0, result, 0, value.Length); return result; }
            private static byte[] Unsigned(sbyte[] value) { var result = new byte[value.Length]; Buffer.BlockCopy(value, 0, result, 0, value.Length); return result; }
        }
#endif
    }
}
