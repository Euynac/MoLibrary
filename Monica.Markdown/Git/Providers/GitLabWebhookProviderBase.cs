using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Git.Modules;

namespace Monica.Markdown.Git.Providers;

/// <summary>
/// Base class for GitLab webhook providers.
/// </summary>
public abstract class GitLabWebhookProviderBase(
    IOptions<ModuleGitOption> options,
    ILogger logger) : GitWebhookProviderBase(logger)
{
    /// <summary>
    /// Gets the configured module options.
    /// </summary>
    protected ModuleGitOption Option { get; } = options.Value;

    /// <inheritdoc />
    public override string ProviderKey => "gitlab";

    /// <inheritdoc />
    public override string RouteSegment => Option.GitLabWebhook.RouteSegment;

    /// <inheritdoc />
    public override async Task<GitWebhookProcessResult> ProcessAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventName = request.Headers["X-Gitlab-Event"].ToString();
            var token = request.Headers["X-Gitlab-Token"].ToString();
            var (_, payload) = await ReadPayloadAsync(request, cancellationToken);

            if (!ValidateToken(token))
            {
                return GitWebhookProcessResult.Rejected(
                    "GitLab webhook token validation failed.",
                    StatusCodes.Status401Unauthorized);
            }

            return await HandleGitLabEventAsync(eventName, payload, cancellationToken);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Invalid GitLab webhook JSON payload.");
            return GitWebhookProcessResult.Rejected(
                "GitLab webhook payload is not valid JSON.",
                StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Handles a validated GitLab webhook event.
    /// </summary>
    protected abstract Task<GitWebhookProcessResult> HandleGitLabEventAsync(
        string eventName,
        JsonDocument payload,
        CancellationToken cancellationToken);

    private bool ValidateToken(string token)
    {
        return string.IsNullOrWhiteSpace(Option.GitLabWebhook.Token)
               || string.Equals(Option.GitLabWebhook.Token, token, StringComparison.Ordinal);
    }
}
