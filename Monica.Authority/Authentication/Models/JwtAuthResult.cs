using System.Text.Json.Serialization;

namespace Monica.Authority.Authentication.Models;

public class JwtAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken => RefreshTokenObj.TokenString;

    /// <summary>
    /// Token类型
    /// </summary>
    public string TokenType => "bearer";
    /// <summary>
    /// AccessToken失效时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    [JsonIgnore]
    public RefreshToken RefreshTokenObj { get; set; } = new();
}