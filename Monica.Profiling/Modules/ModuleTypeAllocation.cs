using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Profiling.TypeAllocation.Facades;
using Monica.Profiling.TypeAllocation.Models;
using Monica.Profiling.TypeAllocation.Providers.ClrMd;
using Monica.Profiling.TypeAllocation.Providers.TraceEvent;
using Monica.Profiling.TypeAllocation.Services;
using Monica.Profiling.TypeAllocation.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the type allocation module.
/// </summary>
public static class ModuleTypeAllocationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the type allocation module.
        /// </summary>
        public static ModuleTypeAllocationGuide AddTypeAllocation(Action<ModuleTypeAllocationOption>? action = null)
        {
            return new ModuleTypeAllocationGuide().Register(action);
        }
    }
}

/// <summary>
/// Type allocation tracking module.
/// </summary>
[ModuleKey(BuiltInModuleKey.TypeAllocation)]
public class ModuleTypeAllocation(ModuleTypeAllocationOption option)
    : ModuleBase<ModuleTypeAllocation, ModuleTypeAllocationOption, ModuleTypeAllocationGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.AutoStartCollection)
        {
            DependsOnModule<ModuleHostedServiceGuide>().Register();
        }
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TraceEventTypeAllocationProvider>>();
            return new TraceEventTypeAllocationProvider(
                logger,
                maxTrackedTypes: Option.MaxTrackedTypes,
                autoStopAfter: Option.AutoStopAfter);
        });
        services.AddSingleton<ClrMdHeapSnapshotProvider>();
        services.AddSingleton<TypeAllocationTrackingService>();
        services.AddScoped(sp => new TypeAllocationFacade(
            sp.GetRequiredService<TypeAllocationTrackingService>(),
            sp.GetRequiredService<ILogger<TypeAllocationFacade>>()));

        if (Option.AutoStartCollection)
        {
            services.AddHostedService<TypeAllocationAutoStartHostedService>();
        }
    }
}

/// <summary>
/// Fluent guide for the type allocation module.
/// </summary>
public class ModuleTypeAllocationGuide
    : ModuleGuide<ModuleTypeAllocation, ModuleTypeAllocationOption, ModuleTypeAllocationGuide>
{
}

/// <summary>
/// Configuration options for the type allocation module.
/// </summary>
public class ModuleTypeAllocationOption : ModuleOptions<ModuleTypeAllocation>
{
    /// <summary>
    /// Automatically starts type allocation collection when the module initializes.
    /// This is useful for unattended diagnostics, but it can increase startup overhead.
    /// </summary>
    public bool AutoStartCollection { get; set; }

    /// <summary>
    /// Selects the allocation sampling mode used when automatic collection starts.
    /// Set this to <see cref="AllocationSamplingMode.Disabled" /> to keep auto-start registration without beginning collection.
    /// </summary>
    public AllocationSamplingMode DefaultSamplingMode { get; set; } = AllocationSamplingMode.High;

    /// <summary>
    /// Caps the number of tracked types kept in the live allocation ranking.
    /// Larger values preserve more fidelity at the cost of additional memory.
    /// </summary>
    public int MaxTrackedTypes { get; set; } = 500;

    /// <summary>
    /// Stops automatic allocation collection after the configured duration.
    /// Leave this as <see langword="null" /> to keep collection running until it is stopped manually.
    /// </summary>
    public TimeSpan? AutoStopAfter { get; set; } = TimeSpan.FromMinutes(10);
}
