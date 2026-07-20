using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.DevOps.Git.Abstractions;
using Monica.DevOps.Git.Models;

namespace Monica.DevOps.Git.Services;

/// <summary>
/// Performs background startup synchronization for configured repositories.
/// </summary>
public sealed class GitStartupSyncHostedService(
    IGitRepositoryService repositoryService,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<GitStartupSyncHostedService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    /// <inheritdoc />
    public override string ServiceName => "GitStartupSyncHostedService";
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.Git);

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
