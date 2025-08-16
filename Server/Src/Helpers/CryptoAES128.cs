using System.Security.Cryptography;
using System.Text;

namespace Server.Helpers;

public static class CryptoAES128
{
    static readonly Aes aes = Aes.Create();

    public static byte[] Encrypt(string plainText, byte[] key, byte[] iv)
    {
        aes.Key = key;       // 16 bytes for AES-128
        aes.IV = iv;         // 16 bytes
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    }

    public static string Decrypt(byte[] cipherBytes, byte[] key, byte[] iv)
    {
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}