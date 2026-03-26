namespace Monica.DevOps.Git.Models;

/// <summary>
/// Configuration for the built-in GitHub webhook provider.
/// </summary>
public class GitHubWebhookOption
{
    /// <summary>
    /// Gets or sets the webhook route segment.
    /// </summary>
    public string RouteSegment { get; set; } = "github";

    /// <summary>
    /// Gets or sets the optional webhook secret used for HMAC validation.
    /// </summary>
    public string? Secret { get; set; }
}
