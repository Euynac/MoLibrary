using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Features.HostedServices;
using Monica.Core.Features.HostedServices.Models;
using Monica.Core.Features.ObservableInstance;
using Monica.Modules;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// Performs background startup synchronization for configured repositories.
/// </summary>
public sealed class GitStartupSyncHostedService(
    IGitRepositoryService repositoryService,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<GitStartupSyncHostedService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    /// <inheritdoc />
    public override string ServiceName => "GitStartupSyncHostedService";

    /// <inheritdoc />
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("Starting Git repository bootstrap synchronization.", HostedServiceState.Executing);

        var results = await repositoryService.SyncAllAsync(GitSyncTrigger.Startup, stoppingToken);
        var successCount = results.Count(x => x.Success);
        var failureCount = results.Count - successCount;

        RecordState(
            $"Git repository bootstrap synchronization completed. Success={successCount}, Failed={failureCount}.",
            HostedServiceState.Running);
    }
}
