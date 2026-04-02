using System.Security.Cryptography;
using System.Text;

namespace Monica.Tool.Security;

/// <summary>
/// Computes deterministic hexadecimal hashes for text input.
/// </summary>
public static class Hashing
{
    /// <summary>
    /// Computes a hexadecimal hash string for the provided input.
    /// </summary>
    /// <param name="input">The input text to hash.</param>
    /// <param name="algorithm">The hash algorithm. MD5 is used when omitted.</param>
    /// <param name="lowercase">Controls whether the output hex text is lowercase.</param>
    public static string ComputeHex(
        string input,
        HashAlgorithmName? algorithm = null,
        bool lowercase = true)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var resolvedAlgorithm = algorithm ?? HashAlgorithmName.MD5;
        var format = lowercase ? "x2" : "X2";
        var builder = new StringBuilder();

        using var hash = IncrementalHash.CreateHash(resolvedAlgorithm);
        hash.AppendData(Encoding.UTF8.GetBytes(input));

        foreach (var value in hash.GetHashAndReset())
        {
            builder.Append(value.ToString(format));
        }

        return builder.ToString();
    }
}
