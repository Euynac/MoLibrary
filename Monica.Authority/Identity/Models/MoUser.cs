using Monica.Authority.Identity.Abstractions;

namespace Monica.Authority.Identity.Models;

public class MoUser : IMoUser
{
    public string? Id { get; set; }
    public string? RoleId { get; set; }
    public string? Nickname { get; set; }
    public string? Username { get; set; }
}