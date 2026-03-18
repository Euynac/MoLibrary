namespace Monica.Authority.Authentication.Models;

public class RefreshToken
{
    public string Username { get; set; } = string.Empty;    // can be used for usage tracking
    // can optionally include other metadata, such as user agent, ip address, device name, and so on

    public string TokenString { get; set; } = string.Empty;

    public DateTime ExpireAt { get; set; }
}