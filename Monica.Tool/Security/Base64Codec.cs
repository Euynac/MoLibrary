using System.Text;

namespace Monica.Tool.Security;

/// <summary>
/// Encodes and decodes Base64 text using a caller-provided encoding.
/// </summary>
public static class Base64Codec
{
    /// <summary>
    /// Encodes text to Base64. Returns the original text when encoding fails.
    /// </summary>
    public static string Encode(string source, Encoding? encoding = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        encoding ??= Encoding.UTF8;

        try
        {
            return Convert.ToBase64String(encoding.GetBytes(source));
        }
        catch
        {
            return source;
        }
    }

    /// <summary>
    /// Decodes Base64 text. Returns the original text when decoding fails.
    /// </summary>
    public static string Decode(string source, Encoding? encoding = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        encoding ??= Encoding.UTF8;

        try
        {
            return encoding.GetString(Convert.FromBase64String(source));
        }
        catch
        {
            return source;
        }
    }
}
