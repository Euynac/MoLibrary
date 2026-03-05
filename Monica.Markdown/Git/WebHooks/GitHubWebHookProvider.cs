using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.WebHooks;

/// <summary>
/// GitHub webhook provider. Validates X-Hub-Signature-256 and parses push event payloads.
/// Non-sealed with virtual methods to allow extension.
/// </summary>
public class GitHubWebHookProvider : IGitWebHookProvider
{
    /// <inheritdoc />
    public string ProviderKey => "github";

    /// <inheritdoc />
    public string? Secret { get; set; }

    /// <inheritdoc />
    public virtual bool ValidateRequest(HttpRequest request)
    {
        if (string.IsNullOrEmpty(Secret))
            return true;

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
            return false;

        var signature = signatureHeader.ToString();
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = reader.ReadToEnd();
        request.Body.Position = 0;

        var keyBytes = Encoding.UTF8.GetBytes(Secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(keyBytes, bodyBytes);
        var expected = "sha256=" + Convert.ToHexStringLower(hash);

        return string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public virtual async Task<GitWebHookPayload?> ParsePayloadAsync(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("X-GitHub-Event", out var eventHeader))
            return null;

        if (!string.Equals(eventHeader.ToString(), "push", StringComparison.OrdinalIgnoreCase))
            return null;

        request.EnableBuffering();
        request.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(request.Body);
        request.Body.Position = 0;

        var root = doc.RootElement;

        var repoUrl = root.TryGetProperty("repository", out var repo)
            && repo.TryGetProperty("clone_url", out var cloneUrl)
                ? cloneUrl.GetString()
                : null;

        // Also try html_url and ssh_url as fallbacks
        if (string.IsNullOrEmpty(repoUrl) && repo.ValueKind == JsonValueKind.Object)
        {
            repoUrl = repo.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() : null;
        }

        var refValue = root.TryGetProperty("ref", out var refProp) ? refProp.GetString() : null;
        var branch = refValue?.Replace("refs/heads/", "", StringComparison.Ordinal);

        string? commitSha = null;
        string? commitMessage = null;
        if (root.TryGetProperty("head_commit", out var headCommit))
        {
            commitSha = headCommit.TryGetProperty("id", out var id) ? id.GetString() : null;
            commitMessage = headCommit.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        }

        var sender = root.TryGetProperty("sender", out var senderProp)
            && senderProp.TryGetProperty("login", out var login)
                ? login.GetString()
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
