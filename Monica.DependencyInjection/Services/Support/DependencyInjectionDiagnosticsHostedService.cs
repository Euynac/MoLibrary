using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;

namespace Monica.DependencyInjection.Services.Support;

/// <summary>
/// Warms the diagnostics snapshot after the host starts building application services.
/// </summary>
internal sealed class DependencyInjectionDiagnosticsHostedService(
    DependencyInjectionDiagnosticsRegistry registry,
    ILogger<DependencyInjectionDiagnosticsHostedService> logger,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory)
    : MoHostedService(observableManager, hostedServiceOptions, serviceScopeFactory, logger)
{
    /// <inheritdoc />
    public override string ServiceName => nameof(DependencyInjectionDiagnosticsHostedService);

    /// <inheritdoc />
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.DependencyInjection);

    /// <inheritdoc />
    protected override Task OnStartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            RecordState("Warming dependency-injection diagnostics snapshot.", HostedServiceState.Executing);
            _ = registry.GetSnapshot();
            RecordState("Dependency-injection diagnostics snapshot warmed.", HostedServiceState.Running);
        }
        catch (Exception ex)
        {
            RecordState("Failed to warm dependency-injection diagnostics snapshot.", HostedServiceState.Degraded, ex);
            logger.LogError(ex, "Failed to warm the dependency-injection diagnostics snapshot during startup.");
        }

        return Task.CompletedTask;
    }
}
