using Microsoft.AspNetCore.Http;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Interfaces;

/// <summary>
/// Processes inbound webhook requests and normalizes them into Git update signals.
/// </summary>
public interface IGitWebhookProvider
{
    /// <summary>
    /// Gets the provider identifier.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the route segment handled by this provider.
    /// </summary>
    string RouteSegment { get; }

    /// <summary>
    /// Processes the request.
    /// </summary>
    Task<GitWebhookProcessResult> ProcessAsync(HttpRequest request, CancellationToken cancellationToken = default);
}
