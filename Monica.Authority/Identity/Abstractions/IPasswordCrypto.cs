using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Monica.Authority.Identity.Abstractions;

/// <summary>
/// Interface for password encryption
/// </summary>
public interface IPasswordCrypto
{
    [return: NotNullIfNotNull(nameof(password))]
    string? Encrypt(string? password);

    [return: NotNullIfNotNull(nameof(password))]
    string? Encrypt(string? password, HashAlgorithmName algorithm);
}
