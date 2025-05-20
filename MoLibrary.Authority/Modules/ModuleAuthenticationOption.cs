using System.Text;
using Microsoft.IdentityModel.Tokens;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthenticationOption : MoModuleControllerOption<ModuleAuthentication>
{
    //巨坑：Secret的长度必须大于128bit，否则需要补全到该长度。而且Secret必须一致，不可动态生成
    public SymmetricSecurityKey SecurityKey => new(Encoding.ASCII.GetBytes(Secret.PadRight(512 / 8, '\0')));
    public string Secret { get; set; } = nameof(Secret) + nameof(Secret);
    /// <summary>
    /// Can not be null or empty if validate Issuer.
    /// </summary>
    public string Issuer { get; set; } = nameof(Issuer);

    /// <summary>
    /// Can not be null or empty if validate audience.
    /// </summary>
    public string Audience { get; set; } = nameof(Audience);

    public int AccessTokenExpiration { get; set; } = 60;

    public int RefreshTokenExpiration { get; set; } = 120;
    public bool IsDebugging { get; set; }
}