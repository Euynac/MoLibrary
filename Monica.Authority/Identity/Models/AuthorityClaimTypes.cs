using System.Security.Claims;

namespace Monica.Authority.Identity.Models;

public static class AuthorityClaimTypes
{
    /// <summary>
    /// Username
    /// </summary>
    public static string Username { get; set; } = ClaimTypes.Name;

    /// <summary>
    /// User nickname
    /// </summary>
    public static string Nickname { get; set; } = "nickname";
    /// <summary>
    /// User ID
    /// </summary>
    public static string UserId { get; set; } = "uid";
    /// <summary>
    /// Role ID
    /// </summary>
    public static string RoleId { get; set; } = "rid";
}
