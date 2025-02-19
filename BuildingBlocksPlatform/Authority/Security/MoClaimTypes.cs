using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Security;

public static class MoClaimTypes
{
    /// <summary>
    /// 用户登录名
    /// </summary>
    public static string Username { get; set; } = ClaimTypes.Name;

    /// <summary>
    /// 用户昵称
    /// </summary>
    public static string Nickname { get; set; } = "nickname";
    /// <summary>
    /// 用户ID
    /// </summary>
    public static string UserId { get; set; } = "uid";
    /// <summary>
    /// 角色ID
    /// </summary>
    public static string RoleId { get; set; } = "rid";
}