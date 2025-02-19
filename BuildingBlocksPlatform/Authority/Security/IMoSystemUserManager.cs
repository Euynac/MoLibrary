using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Security;

/// <summary>
/// 系统用户管理接口
/// </summary>
public interface IMoSystemUserManager
{
    /// <summary>
    /// 获取系统用户Token
    /// </summary>
    /// <returns></returns>
    public string GetTokenOfSystemUser();
    /// <summary>
    /// 获取系统用户Claims
    /// </summary>
    /// <returns></returns>
    public List<Claim> GetSystemUserClaims();
    /// <summary>
    /// 获取系统用户Principal
    /// </summary>
    /// <returns></returns>
    public ClaimsPrincipal GetSystemUserPrinciple();
}