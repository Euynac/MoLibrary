using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.WebHooks;

/// <summary>
/// GitLab webhook provider. Validates X-Gitlab-Token and parses push event payloads.
/// Non-sealed with virtual methods to allow extension.
/// </summary>
public class GitLabWebHookProvider : IGitWebHookProvider
{
    /// <inheritdoc />
    public string ProviderKey => "gitlab";

    /// <inheritdoc />
    public string? Secret { get; set; }

    /// <inheritdoc />
    public virtual bool ValidateRequest(HttpRequest request)
    {
        if (string.IsNullOrEmpty(Secret))
            return true;

        if (!request.Headers.TryGetValue("X-Gitlab-Token", out var tokenHeader))
            return false;

        return string.Equals(tokenHeader.ToString(), Secret, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public virtual async Task<GitWebHookPayload?> ParsePayloadAsync(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("X-Gitlab-Event", out var eventHeader))
            return null;

        if (!string.Equals(eventHeader.ToString(), "Push Hook", StringComparison.OrdinalIgnoreCase))
            return null;

        request.EnableBuffering();
        request.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(request.Body);
        request.Body.Position = 0;

        var root = doc.RootElement;

        var repoUrl = root.TryGetProperty("repository", out var repo)
            && repo.TryGetProperty("git_http_url", out var httpUrl)
                ? httpUrl.GetString()
                : null;

        if (string.IsNullOrEmpty(repoUrl) && root.TryGetProperty("project", out var project))
        {
            repoUrl = project.TryGetProperty("git_http_url", out var projUrl) ? projUrl.GetString() : null;
        }

        var refValue = root.TryGetProperty("ref", out var refProp) ? refProp.GetString() : null;
        var branch = refValue?.Replace("refs/heads/", "", StringComparison.Ordinal);

        string? commitSha = null;
        string? commitMessage = null;
        if (root.TryGetProperty("commits", out var commits) && commits.GetArrayLength() > 0)
        {
            var lastCommit = commits[commits.GetArrayLength() - 1];
            commitSha = lastCommit.TryGetProperty("id", out var id) ? id.GetString() : null;
            commitMessage = lastCommit.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        }

        var sender = root.TryGetProperty("user_name", out var userName)
            ? userName.GetString()
            : null;

        if (string.IsNullOrEmpty(repoUrl) || string.IsNullOrEmpty(branch))
            return null;

        return new GitWebHookPayload
        {
            RepositoryUrl = repoUrl,
            Branch = branch,
            CommitSha = commitSha,
            CommitMessage = commitMessage,
            Sender = sender
        };
    }
}
