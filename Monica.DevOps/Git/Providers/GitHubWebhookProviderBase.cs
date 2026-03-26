using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.Git.Models;
using Monica.Modules;

namespace Monica.DevOps.Git.Providers;

/// <summary>
/// Base class for GitHub webhook providers.
/// </summary>
public abstract class GitHubWebhookProviderBase(
    IOptions<ModuleGitOption> options,
    ILogger logger) : GitWebhookProviderBase(logger)
{
    /// <summary>
    /// Gets the configured module options.
    /// </summary>
    protected ModuleGitOption Option { get; } = options.Value;

    /// <inheritdoc />
    public override string ProviderKey => "github";

    /// <inheritdoc />
    public override string RouteSegment => Option.GitHubWebhook.RouteSegment;

    /// <inheritdoc />
    public override async Task<GitWebhookProcessResult> ProcessAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventName = request.Headers["X-GitHub-Event"].ToString();
            var (rawBody, payload) = await ReadPayloadAsync(request, cancellationToken);

            if (!ValidateSignature(request, rawBody))
            {
                return GitWebhookProcessResult.Rejected(
                    "GitHub webhook signature validation failed.",
                    StatusCodes.Status401Unauthorized);
            }

            return await HandleGitHubEventAsync(eventName, payload, cancellationToken);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Invalid GitHub webhook JSON payload.");
            return GitWebhookProcessResult.Rejected(
                "GitHub webhook payload is not valid JSON.",
                StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Handles a validated GitHub webhook event.
    /// </summary>
    protected abstract Task<GitWebhookProcessResult> HandleGitHubEventAsync(
        string eventName,
        JsonDocument payload,
        CancellationToken cancellationToken);

    private bool ValidateSignature(HttpRequest request, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(Option.GitHubWebhook.Secret))
        {
            return true;
        }

        var signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Option.GitHubWebhook.Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expected = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
