using System.Security.Claims;
using Microsoft.Extensions.Options;
using MoLibrary.Authority.Authentication;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Authority.Security;

/// <summary>
/// 当前系统用户管理接口
/// </summary>
public interface IMoSystemUserManager
{
    /// <summary>
    /// 判断当前用户信息是否是系统用户
    /// </summary>
    /// <param name="userInfo"></param>
    /// <returns></returns>
    public bool IsSystemUser(IMoUser userInfo);
    /// <summary>
    /// 获取当前系统用户Token
    /// </summary>
    /// <returns></returns>
    public string GetTokenOfCurSystemUser();
    /// <summary>
    /// 获取当前系统用户Claims
    /// </summary>
    /// <returns></returns>
    public List<Claim> GetCurSystemUserClaims();
    /// <summary>
    /// 获取当前系统用户Principal
    /// </summary>
    /// <returns></returns>
    public ClaimsPrincipal GetCurSystemUserPrinciple();
    MoSystemUserOptions.SystemUserInfo GetSystemUserInfo<T>(T userEnum) where T : struct, Enum;
    string GetTokenOfSystemUser<T>(T userEnum) where T : struct, Enum;
    List<Claim> GetSystemUserClaims<T>(T userEnum) where T : struct, Enum;
    ClaimsPrincipal GetSystemUserPrinciple<T>(T userEnum) where T : struct, Enum;
    IEnumerable<MoSystemUserOptions.SystemUserInfo> GetAllSystemUserInfos();
}

public enum EMoDefaultSystemUser
{
    System = 0
}

public class MoSystemUserOptions
{
    public Type? SystemUserEnums { get; private set; } 
    public object? CurrentSystemUserEnum { get; set; }
    public Dictionary<object, SystemUserInfo> InfoDict { get; set; } = [];

    public class SystemUserInfo
    {
        public required string Username { get; set; }
        public required string UserId { get; set; }
        public required string NickName { get; set; }
    }

    public void SetCurSystemUser<T>(T curSystemUser) where T : struct, Enum
    {
        SystemUserEnums = typeof(T);
        CurrentSystemUserEnum = curSystemUser;
        foreach (var (i, key) in Enum.GetValues<T>().WithIndex())
        {
            var index = i + 1;
            InfoDict.Add(key, new SystemUserInfo
            {
                Username = key.ToString(),
                UserId = Guid.Empty.ToString()[..^index.ToString().Length] + index,
                NickName = key.GetDescription()!
            });
        }

    }

    
}


public class MoSystemUserManager(IMoAuthManager manager, IOptions<MoSystemUserOptions> options) : IMoSystemUserManager
{
    private readonly MoSystemUserOptions _options = options.Value;

    public MoSystemUserOptions.SystemUserInfo GetCurSystemUserInfo()
    {
        if (_options.CurrentSystemUserEnum == null) throw new InvalidOperationException("未设置当前系统用户！");

        return GetSystemUserInfoBase(_options.CurrentSystemUserEnum);
    }
    private MoSystemUserOptions.SystemUserInfo GetSystemUserInfoBase(object userEnum)
    {
        if (_options.InfoDict.TryGetValue(userEnum, out var systemUserInfo))
        {
            return systemUserInfo;
        }

        throw new InvalidOperationException($"未设置当前枚举{userEnum.GetType().FullName}为系统用户枚举！");
    }



    public MoSystemUserOptions.SystemUserInfo GetSystemUserInfo<T>(T userEnum) where T : struct, Enum
    {
        return GetSystemUserInfoBase(userEnum);
    }

    public IEnumerable<MoSystemUserOptions.SystemUserInfo> GetAllSystemUserInfos()
    {
        return _options.InfoDict.Values;
    }

    public bool IsSystemUser(IMoUser userInfo)
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
            new(MoClaimTypes.Username, user.Username),
            new(MoClaimTypes.UserId,user.UserId),
            new(MoClaimTypes.Nickname, user.NickName),
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