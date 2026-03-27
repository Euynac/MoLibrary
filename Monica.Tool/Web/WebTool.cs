using System.Security.Cryptography;
using System.Text;

namespace Monica.Tool.Web;

/// <summary>
/// Network and security utility helpers.
/// </summary>
public class WebTool
{
    /// <summary>
    /// Encodes text to Base64. Returns the original text if encoding fails. UTF-8 is used by default.
    /// </summary>
    /// <param name="source">Source text.</param>
    /// <param name="codeType">Text encoding.</param>
    /// <returns></returns>
    public static string EncodeBase64(string source, Encoding? codeType = null)
    {
        if (string.IsNullOrEmpty(source)) return "";
        codeType ??= Encoding.UTF8;
        string encode;
        try
        {
            var bytes = codeType.GetBytes(source);
            encode = Convert.ToBase64String(bytes);
        }
        catch
        {
            encode = source;
        }
        return encode;
    }

    /// <summary>
    /// Decodes Base64 text. Returns the original text if decoding fails. UTF-8 is used by default.
    /// </summary>
    /// <param name="source">Source text.</param>
    /// <param name="codeType">Text encoding. UTF-8 is used when null.</param>
    /// <returns></returns>
    public static string DecodeBase64(string source, Encoding? codeType = null)
    {
        if (string.IsNullOrEmpty(source)) return "";
        codeType ??= Encoding.UTF8;
        string decode;
        try
        {
            var bytes = Convert.FromBase64String(source);
            decode = codeType.GetString(bytes);
        }
        catch
        {
            decode = source;
        }
        return decode;
    }


    /// <summary>
    /// Compute string hash use specific hash algorithm.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="hashAlgorithm">Default is use MD5</param>
    /// <returns></returns>
    public static string StringHash(string str, HashAlgorithmName? hashAlgorithm = null)
    {
        if (string.IsNullOrEmpty(str)) return "";
        var resolvedHashAlgorithm = hashAlgorithm ?? HashAlgorithmName.MD5;
        var sb = new StringBuilder();
        var enc = Encoding.UTF8;
        using var hash = IncrementalHash.CreateHash(resolvedHashAlgorithm);
        hash.AppendData(enc.GetBytes(str));
        var result = hash.GetHashAndReset();
        foreach (var b in result)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Advance version in https://github.com/tmenier/Flurl</remarks>
    /// <param name="urlBase"></param>
    /// <param name="urlAppend"></param>
    /// <returns></returns>
    public static string Combine(string urlBase, string urlAppend) => $"{urlBase.TrimEnd('/')}/{urlAppend.TrimStart('/')}";
}
