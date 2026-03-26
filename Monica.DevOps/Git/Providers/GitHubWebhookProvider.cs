using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.Git.Models;
using Monica.Modules;

namespace Monica.DevOps.Git.Providers;

/// <summary>
/// Built-in GitHub webhook provider supporting push events.
/// </summary>
public class GitHubWebhookProvider(
    IOptions<ModuleGitOption> options,
    ILogger<GitHubWebhookProvider> logger)
    : GitHubWebhookProviderBase(options, logger)
{
    /// <inheritdoc />
    protected override Task<GitWebhookProcessResult> HandleGitHubEventAsync(
        string eventName,
        JsonDocument payload,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(eventName, "push", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(GitWebhookProcessResult.Accepted(
                $"GitHub event '{eventName}' is ignored by the default provider."));
        }

        var remoteUrl = GetNestedString(payload.RootElement, "repository", "clone_url")
                        ?? GetNestedString(payload.RootElement, "repository", "html_url")
                        ?? GetNestedString(payload.RootElement, "repository", "ssh_url");

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return Task.FromResult(GitWebhookProcessResult.Rejected(
                "GitHub push payload does not contain a repository URL.",
                StatusCodes.Status400BadRequest));
        }

        var branch = NormalizeBranch(GetNestedString(payload.RootElement, "ref"));
        return Task.FromResult(GitWebhookProcessResult.SyncRequested(
            remoteUrl,
            branch,
            eventName,
            $"GitHub push webhook accepted for branch '{branch ?? "unknown"}'."));
    }
}
