using Microsoft.AspNetCore.Http;

namespace Monica.DevOps.Git.Models;

/// <summary>
/// Normalized webhook provider processing result.
/// </summary>
public sealed class GitWebhookProcessResult
{
    /// <summary>
    /// Gets the HTTP status code to return.
    /// </summary>
    public int StatusCode { get; init; } = StatusCodes.Status202Accepted;

    /// <summary>
    /// Gets whether the webhook should trigger synchronization.
    /// </summary>
    public bool ShouldSync { get; init; }

    /// <summary>
    /// Gets the matched repository URL from the webhook payload.
    /// </summary>
    public string? RepositoryUrl { get; init; }

    /// <summary>
    /// Gets the branch name extracted from the payload.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the source event name.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// Gets the human-readable result message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Creates a result indicating the webhook was accepted but ignored.
    /// </summary>
    public static GitWebhookProcessResult Accepted(string message)
    {
        return new GitWebhookProcessResult
        {
            Message = message,
            StatusCode = StatusCodes.Status202Accepted
        };
    }

    /// <summary>
    /// Creates a result indicating the request is invalid.
    /// </summary>
    public static GitWebhookProcessResult Rejected(string message, int statusCode)
    {
        return new GitWebhookProcessResult
        {
            Message = message,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Creates a result indicating synchronization should be started.
    /// </summary>
    public static GitWebhookProcessResult SyncRequested(
        string repositoryUrl,
        string? branch,
        string? eventName,
        string message)
    {
        return new GitWebhookProcessResult
        {
            ShouldSync = true,
            RepositoryUrl = repositoryUrl,
            Branch = branch,
            EventName = eventName,
            Message = message,
            StatusCode = StatusCodes.Status202Accepted
        };
    }
}
