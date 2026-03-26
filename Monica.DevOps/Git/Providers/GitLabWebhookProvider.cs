using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.Git.Models;
using Monica.Modules;

namespace Monica.DevOps.Git.Providers;

/// <summary>
/// Built-in GitLab webhook provider supporting push events.
/// </summary>
public class GitLabWebhookProvider(
    IOptions<ModuleGitOption> options,
    ILogger<GitLabWebhookProvider> logger)
    : GitLabWebhookProviderBase(options, logger)
{
    /// <inheritdoc />
    protected override Task<GitWebhookProcessResult> HandleGitLabEventAsync(
        string eventName,
        JsonDocument payload,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(eventName, "Push Hook", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(GitWebhookProcessResult.Accepted(
                $"GitLab event '{eventName}' is ignored by the default provider."));
        }

        var remoteUrl = GetNestedString(payload.RootElement, "project", "git_http_url")
                        ?? GetNestedString(payload.RootElement, "project", "web_url")
                        ?? GetNestedString(payload.RootElement, "project", "git_ssh_url")
                        ?? GetNestedString(payload.RootElement, "repository", "homepage");

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return Task.FromResult(GitWebhookProcessResult.Rejected(
                "GitLab push payload does not contain a repository URL.",
                StatusCodes.Status400BadRequest));
        }

        var branch = NormalizeBranch(GetNestedString(payload.RootElement, "ref"));
        return Task.FromResult(GitWebhookProcessResult.SyncRequested(
            remoteUrl,
            branch,
            eventName,
            $"GitLab push webhook accepted for branch '{branch ?? "unknown"}'."));
    }
}
