using System.Security.Cryptography;
using Monica.Authority.Identity.Abstractions;
using Monica.Tool.Security;

namespace Monica.Authority.Identity.Services;

public class PasswordCrypto : IPasswordCrypto
{
   
    public string? Encrypt(string? password)
    {
        return password == null ? null : Hashing.ComputeHex(password, HashAlgorithmName.MD5);
    }

    public string? Encrypt(string? password, HashAlgorithmName algorithm)
    {
        return password == null ? null : Hashing.ComputeHex(password, algorithm);
    }
}
