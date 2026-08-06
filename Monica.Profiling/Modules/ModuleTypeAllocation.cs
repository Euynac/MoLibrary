using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the type allocation module.
        /// </summary>
        public ModuleRegistration<ModuleTypeAllocation, ModuleTypeAllocationOption> AddTypeAllocation(
            Action<ModuleTypeAllocationOption>? action = null)
        {
            return builder.AddModule<ModuleTypeAllocation, ModuleTypeAllocationOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleTypeAllocation, ModuleTypeAllocationOption> registration)
    {
        /// <summary>
        /// Starts allocation collection with the host and registers the hosted-service dependency required for it.
        /// </summary>
        /// <param name="samplingMode">The sampling mode used when automatic collection starts.</param>
        /// <returns>The same host-bound registration.</returns>
        public ModuleRegistration<ModuleTypeAllocation, ModuleTypeAllocationOption> StartAutomatically(
            AllocationSamplingMode samplingMode = AllocationSamplingMode.High)
        {
            registration.Require<ModuleHostedService, ModuleHostedServiceOption>();
            return registration.Configure(options =>
            {
                options.AutoStartCollection = true;
                options.DefaultSamplingMode = samplingMode;
            });
        }
    }
}

/// <summary>
/// Type allocation tracking module.
/// </summary>
public class ModuleTypeAllocation : MonicaModule<ModuleTypeAllocationOption>
{
    /// <inheritdoc />
    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleTypeAllocationOption> context)
    {
        var services = context.Services;
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
/// Configuration options for the type allocation module.
/// </summary>
public class ModuleTypeAllocationOption : ModuleOptions<ModuleTypeAllocation>
{
    /// <summary>
    /// Automatically starts type allocation collection when the module initializes.
    /// This is useful for unattended diagnostics, but it can increase startup overhead.
    /// </summary>
    public bool AutoStartCollection { get; internal set; }

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
