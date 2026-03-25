using System.Security.Claims;
using Microsoft.Extensions.Options;
using Monica.Authority.Authentication.Abstractions;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Models;

namespace Monica.Authority.Identity.Services;

internal class SystemUserManager(IAccessTokenIssuer manager, IOptions<SystemUserOptions> options) : ISystemUserManager
{
    private readonly SystemUserOptions _options = options.Value;

    public SystemUserOptions.SystemUserInfo GetCurSystemUserInfo()
    {
        if (_options.CurrentSystemUserEnum == null) throw new InvalidOperationException("未设置当前系统用户！");

        return GetSystemUserInfoBase(_options.CurrentSystemUserEnum);
    }
    private SystemUserOptions.SystemUserInfo GetSystemUserInfoBase(object userEnum)
    {
        if (_options.InfoDict.TryGetValue(userEnum, out var systemUserInfo))
        {
            return systemUserInfo;
        }

        throw new InvalidOperationException($"未设置当前枚举{userEnum.GetType().FullName}为系统用户枚举！");
    }



    public SystemUserOptions.SystemUserInfo GetSystemUserInfo<T>(T userEnum) where T : struct, Enum
    {
        return GetSystemUserInfoBase(userEnum);
    }

    public IEnumerable<SystemUserOptions.SystemUserInfo> GetAllSystemUserInfos()
    {
        return _options.InfoDict.Values;
    }

    public bool IsSystemUser(IAuthorityUser userInfo)
    {
        if (userInfo.Id is { } id && id.StartsWith("00000000-0000-0000-0000-"))
        {
            return true;
        }

        return false;
    }

    public string GetTokenOfCurSystemUser()
    {
        var list = GetCurSystemUserClaims();
        var user = GetCurSystemUserInfo();
        return manager.GenerateTokens(user.Username, [.. list], DateTime.Now);
    }
    public string GetTokenOfSystemUser<T>(T userEnum) where T : struct, Enum
    {
        var list = GetSystemUserClaims(userEnum);
        var user = GetSystemUserInfo(userEnum);
        return manager.GenerateTokens(user.Username, [.. list], DateTime.Now);
    }
    public List<Claim> GetCurSystemUserClaims()
    {
        if (_options.CurrentSystemUserEnum == null) throw new InvalidOperationException("未设置当前系统用户！");
        return GetSystemUserClaimsBase(_options.CurrentSystemUserEnum);
    }
    private List<Claim> GetSystemUserClaimsBase(object userEnum)
    {
        var user = GetSystemUserInfoBase(userEnum);
        var list = new List<Claim>
        {
            new(AuthorityClaimTypes.Username, user.Username),
            new(AuthorityClaimTypes.UserId,user.UserId),
            new(AuthorityClaimTypes.Nickname, user.NickName),
        };
        return list;
    }
    public List<Claim> GetSystemUserClaims<T>(T userEnum) where T : struct, Enum
    {
        return GetSystemUserClaimsBase(userEnum);
    }

    public ClaimsPrincipal GetCurSystemUserPrinciple()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(GetCurSystemUserClaims(), "auto"));
    }
    public ClaimsPrincipal GetSystemUserPrinciple<T>(T userEnum) where T : struct, Enum
    {
        return GetSystemUserPrincipleBase(userEnum);
    }
    private ClaimsPrincipal GetSystemUserPrincipleBase(object userEnum)
    {
        var user = GetSystemUserClaimsBase(userEnum);
        return new ClaimsPrincipal(new ClaimsIdentity(user, "auto"));
    }
}