using System.Security.Cryptography;
using System.Text;

namespace Server.Helpers;

public static class XOR
{
    public static string XorCipher(string text, string key)
    {
        var output = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            // XOR each character with the corresponding key character (loop key if shorter)
            char encryptedChar = (char)(text[i] ^ key[i % key.Length]);
            output.Append(encryptedChar);
        }
        return output.ToString();
    }
}