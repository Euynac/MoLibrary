using System.Security.Claims;

namespace Monica.Authority.Identity.Models;

public static class AuthorityClaimTypes
{
    /// <summary>
    /// Username
    /// </summary>
    public const string Username = ClaimTypes.Name;

    /// <summary>
    /// User nickname
    /// </summary>
    public const string Nickname = "nickname";
    /// <summary>
    /// User ID
    /// </summary>
    public const string UserId = "uid";
    /// <summary>
    /// Role ID
    /// </summary>
    public const string RoleId = "rid";
}
