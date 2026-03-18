using System.Security.Cryptography;
using Monica.Authority.Identity.Abstractions;
using Monica.Tool.Web;

namespace Monica.Authority.Identity.Services;

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