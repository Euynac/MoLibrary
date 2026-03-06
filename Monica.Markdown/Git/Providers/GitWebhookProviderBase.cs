using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Providers;

/// <summary>
/// Base class for webhook providers.
/// </summary>
public abstract class GitWebhookProviderBase(ILogger logger) : IGitWebhookProvider
{
    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <inheritdoc />
    public abstract string ProviderKey { get; }

    /// <inheritdoc />
    public abstract string RouteSegment { get; }

    /// <inheritdoc />
    public abstract Task<GitWebhookProcessResult> ProcessAsync(HttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses the request body as JSON.
    /// </summary>
    protected static async Task<(string RawBody, JsonDocument Json)> ReadPayloadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var json = JsonDocument.Parse(rawBody);
        return (rawBody, json);
    }

    /// <summary>
    /// Reads a nested string property.
    /// </summary>
    protected static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    /// <summary>
    /// Normalizes a ref string into a branch name.
    /// </summary>
    protected static string? NormalizeBranch(string? gitRef)
    {
        if (string.IsNullOrWhiteSpace(gitRef))
        {
            return null;
        }

        const string prefix = "refs/heads/";
        return gitRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? gitRef[prefix.Length..]
            : gitRef;
    }
}
