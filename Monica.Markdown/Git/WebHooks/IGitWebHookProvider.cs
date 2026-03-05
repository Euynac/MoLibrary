using Microsoft.AspNetCore.Http;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.WebHooks;

/// <summary>
/// Provider interface for parsing and validating Git hosting webhook requests.
/// Each implementation handles a specific hosting platform (GitHub, GitLab, etc.).
/// </summary>
public interface IGitWebHookProvider
{
    /// <summary>
    /// Unique key identifying this provider (e.g. "github", "gitlab").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Optional webhook secret used for request signature validation.
    /// </summary>
    string? Secret { get; set; }

    /// <summary>
    /// Validates the incoming request signature or token.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>True if the request is valid; false otherwise.</returns>
    bool ValidateRequest(HttpRequest request);

    /// <summary>
    /// Parses the incoming webhook request into a normalized payload model.
    /// Returns null if the event type is not a push event.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>Normalized payload, or null if the event is not relevant.</returns>
    Task<GitWebHookPayload?> ParsePayloadAsync(HttpRequest request);
}
