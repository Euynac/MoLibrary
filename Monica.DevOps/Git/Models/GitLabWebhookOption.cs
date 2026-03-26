namespace Monica.DevOps.Git.Models;

/// <summary>
/// Configuration for the built-in GitLab webhook provider.
/// </summary>
public class GitLabWebhookOption
{
    /// <summary>
    /// Gets or sets the webhook route segment.
    /// </summary>
    public string RouteSegment { get; set; } = "gitlab";

    /// <summary>
    /// Gets or sets the optional shared token used for request validation.
    /// </summary>
    public string? Token { get; set; }
}
