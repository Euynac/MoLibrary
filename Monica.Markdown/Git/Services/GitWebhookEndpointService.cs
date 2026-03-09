using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;
using Monica.Modules;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// Handles webhook routing and synchronization orchestration.
/// </summary>
public sealed class GitWebhookEndpointService(
    IEnumerable<IGitWebhookProvider> webhookProviders,
    IGitRepositoryService repositoryService,
    IOptions<ModuleGitOption> options)
{
    private readonly ModuleGitOption _option = options.Value;
    private readonly Dictionary<string, IGitWebhookProvider> _providers = webhookProviders
        .GroupBy(x => x.RouteSegment, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Processes a webhook request.
    /// </summary>
    public async Task<IResult> HandleAsync(
        string routeSegment,
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(routeSegment, out var provider))
        {
            return Results.Json(new
            {
                message = $"Webhook provider '{routeSegment}' is not registered."
            }, statusCode: StatusCodes.Status404NotFound);
        }

        if (!_option.IsSyncTriggerEnabled(GitSyncTrigger.Webhook))
        {
            return Results.Json(new
            {
                message = "Git webhook synchronization is disabled by module configuration.",
                provider = provider.ProviderKey,
                route = provider.RouteSegment,
                shouldSync = false
            }, statusCode: StatusCodes.Status202Accepted);
        }

        var providerResult = await provider.ProcessAsync(request, cancellationToken);
        if (!providerResult.ShouldSync || string.IsNullOrWhiteSpace(providerResult.RepositoryUrl))
        {
            return Results.Json(new
            {
                message = providerResult.Message,
                provider = provider.ProviderKey,
                route = provider.RouteSegment,
                eventName = providerResult.EventName,
                shouldSync = false
            }, statusCode: providerResult.StatusCode);
        }

        var repository = await repositoryService.FindRepositoryByRemoteUrlAsync(providerResult.RepositoryUrl, cancellationToken);
        if (repository is null)
        {
            return Results.Json(new
            {
                message = $"No configured repository matches remote URL '{providerResult.RepositoryUrl}'.",
                provider = provider.ProviderKey,
                route = provider.RouteSegment,
                eventName = providerResult.EventName,
                shouldSync = false
            }, statusCode: StatusCodes.Status404NotFound);
        }

        var syncResult = await repositoryService.SyncRepositoryAsync(repository.Id, GitSyncTrigger.Webhook, cancellationToken);
        return Results.Json(new
        {
            message = syncResult.Message,
            provider = provider.ProviderKey,
            route = provider.RouteSegment,
            eventName = providerResult.EventName,
            repositoryId = repository.Id,
            syncResult.Trigger,
            syncResult.Success,
            syncResult.HasChanges,
            syncResult.PreviousCommit,
            syncResult.CurrentCommit,
            webhookBranch = providerResult.Branch
        }, statusCode: syncResult.Success ? StatusCodes.Status202Accepted : StatusCodes.Status500InternalServerError);
    }
}
