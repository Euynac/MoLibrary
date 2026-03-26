namespace Monica.Authority.Identity.Abstractions;

public interface IAuthorityUser
{
    /// <summary>
    /// User Id
    /// </summary>
    string? Id { get; }
    /// <summary>
    /// Role Id
    /// </summary>
    string? RoleId { get; }
    /// <summary>
    /// User nickname
    /// </summary>
    string? Nickname { get; }
    /// <summary>
    /// Username
    /// </summary>
    string? Username { get; }
}
