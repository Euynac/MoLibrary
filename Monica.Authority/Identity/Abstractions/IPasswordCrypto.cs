using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Monica.Authority.Identity.Abstractions;

/// <summary>
/// 密码加密接口
/// </summary>
public interface IPasswordCrypto
{
    [return: NotNullIfNotNull(nameof(password))]
    string? Encrypt(string? password);

    [return: NotNullIfNotNull(nameof(password))]
    string? Encrypt(string? password, HashAlgorithmName algorithm);
}