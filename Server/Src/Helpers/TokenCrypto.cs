using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Server.Helpers;

/// <summary>
/// Authenticated token crypto used for the HTTP/WebSocket auth. Uses AES-256-GCM with a key
/// derived from the shared password via PBKDF2-HMAC-SHA256. The wire format is
/// base64( iv(12) || ciphertext || tag(16) ); the plaintext is an ISO-8601 UTC timestamp.
/// These primitives and parameters are identical in Web Crypto (JS) and
/// System.Security.Cryptography (.NET), so the frontend and server interoperate.
/// </summary>
public static class TokenCrypto
{
    private static readonly byte[] Salt = Convert.FromBase64String("TWVkaWFDb250cm9sU2FsdA==");
    private const int Iterations = 100_000;
    private const int IvSize = 12;
    private const int TagSize = 16;

    private static readonly ConcurrentDictionary<string, byte[]> KeyCache = new();

    private static byte[] GetKey(string password) =>
        KeyCache.GetOrAdd(password, p => Rfc2898DeriveBytes.Pbkdf2(p, Salt, Iterations, HashAlgorithmName.SHA256, 32));

    public static string Encrypt(string password, string plaintext)
    {
        var key = GetKey(password);
        var plain = Encoding.UTF8.GetBytes(plaintext);

        var iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);
        var ciphertext = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(iv, plain, ciphertext, tag);

        var data = new byte[IvSize + ciphertext.Length + TagSize];
        iv.CopyTo(data, 0);
        ciphertext.CopyTo(data, IvSize);
        tag.CopyTo(data, IvSize + ciphertext.Length);
        return Convert.ToBase64String(data);
    }

    public static bool TryDecrypt(string password, string token, out string plaintext)
    {
        plaintext = string.Empty;
        try
        {
            var key = GetKey(password);
            var data = Convert.FromBase64String(token);
            if (data.Length < IvSize + TagSize)
                return false;

            var iv = data.AsSpan(0, IvSize).ToArray();
            var tag = data.AsSpan(data.Length - TagSize).ToArray();
            var ciphertext = data.AsSpan(IvSize, data.Length - IvSize - TagSize).ToArray();

            using var aes = new AesGcm(key, TagSize);
            var plain = new byte[ciphertext.Length];
            aes.Decrypt(iv, ciphertext, tag, plain);

            plaintext = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
