using System.Security.Claims;
using Monica.Authority.Identity.Models;

namespace Monica.Authority.Identity.Abstractions;

/// <summary>
/// 当前系统用户管理接口
/// </summary>
public interface ISystemUserManager
{
    /// <summary>
    /// 判断当前用户信息是否是系统用户
    /// </summary>
    /// <param name="userInfo"></param>
    /// <returns></returns>
    public bool IsSystemUser(IAuthorityUser userInfo);
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
    SystemUserOptions.SystemUserInfo GetSystemUserInfo<T>(T userEnum) where T : struct, Enum;
    string GetTokenOfSystemUser<T>(T userEnum) where T : struct, Enum;
    List<Claim> GetSystemUserClaims<T>(T userEnum) where T : struct, Enum;
    ClaimsPrincipal GetSystemUserPrinciple<T>(T userEnum) where T : struct, Enum;
    IEnumerable<SystemUserOptions.SystemUserInfo> GetAllSystemUserInfos();
}