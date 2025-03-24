using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using MoLibrary.Tool.Web;

namespace MoLibrary.Authority.Security;

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

public class PasswordCrypto : IPasswordCrypto
{
   
    public string? Encrypt(string? password)
    {
        return password == null ? null : WebTool.StringHash(password, HashAlgorithmName.MD5);
    }

    public string? Encrypt(string? password, HashAlgorithmName algorithm)
    {
        return password == null ? null : WebTool.StringHash(password, algorithm);
    }
}