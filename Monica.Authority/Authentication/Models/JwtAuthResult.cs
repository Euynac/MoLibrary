using System.Text.Json.Serialization;

namespace Monica.Authority.Authentication.Models;

public class JwtAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken => RefreshTokenObj.TokenString;

    /// <summary>
    /// Token type
    /// </summary>
    public string TokenType => "bearer";
    /// <summary>
    /// Expiration time of the AccessToken
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    [JsonIgnore]
    public RefreshToken RefreshTokenObj { get; set; } = new();
}
