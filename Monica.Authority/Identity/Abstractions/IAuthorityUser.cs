namespace Monica.Authority.Identity.Abstractions;

public interface IAuthorityUser
{
    /// <summary>
    /// 用户Id
    /// </summary>
    string? Id { get; }
    /// <summary>
    /// 角色Id
    /// </summary>
    string? RoleId { get; }
    /// <summary>
    /// 用户昵称
    /// </summary>
    string? Nickname { get; }
    /// <summary>
    /// 用户登录名
    /// </summary>
    string? Username { get; }
}