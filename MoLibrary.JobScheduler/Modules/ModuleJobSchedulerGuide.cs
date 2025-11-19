using MoLibrary.Core.Module.Interfaces;
using MoLibrary.JobScheduler.Abstractions;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Fluent configuration builder for the Job Scheduler module.
/// </summary>
/// <example>
/// <code>
/// builder.ConfigMoJobScheduler()
///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;()
///     .SetMaxWorkerExecutionThreads(10)
///     .EnableRecurringJobDebugMode();
/// </code>
/// </example>
public class ModuleJobSchedulerGuide
    : MoModuleGuide<ModuleJobScheduler, ModuleJobSchedulerOption, ModuleJobSchedulerGuide>
{
    /// <summary>
    /// Configures a custom metadata store implementation for job persistence.
    /// Defaults to in-memory provider if not specified.
    /// </summary>
    /// <typeparam name="TStore">The metadata store type implementing <see cref="IMoJobScheduleMetadataStore"/>.</typeparam>
    /// <returns>The current guide instance for method chaining.</returns>
    /// <remarks>
    /// Custom stores must be thread-safe and provide atomic state transitions.
    /// </remarks>
    public ModuleJobSchedulerGuide UseCustomMetadataStore<TStore>()
        where TStore : IMoJobScheduleMetadataStore
    {
        return ConfigureModuleOption(options =>
        {
            options.CustomMetadataStoreType = typeof(TStore);
        });
    }

    /// <summary>
    /// Enables recurring job debug mode, preventing automatic cron-based scheduling.
    /// Jobs can still be triggered manually for testing.
    /// </summary>
    /// <param name="enabled">Whether to enable debug mode. Default is <c>true</c>.</param>
    /// <returns>The current guide instance for method chaining.</returns>
    public ModuleJobSchedulerGuide EnableRecurringJobDebugMode(bool enabled = true)
    {
        return ConfigureModuleOption(options =>
        {
            options.RecurringJobDebugMode = enabled;
        });
    }

    /// <summary>
    /// Enables triggered job debug mode, preventing automatic execution after EnqueueAsync.
    /// Job instances are created but not published to the event bus.
    /// </summary>
    /// <param name="enabled">Whether to enable debug mode. Default is <c>true</c>.</param>
    /// <returns>The current guide instance for method chaining.</returns>
    public ModuleJobSchedulerGuide EnableTriggeredJobDebugMode(bool enabled = true)
    {
        return ConfigureModuleOption(options =>
        {
            options.TriggeredJobDebugMode = enabled;
        });
    }

    /// <summary>
    /// Sets the maximum number of concurrent job executions allowed on this worker instance.
    /// </summary>
    /// <param name="maxThreads">
    /// Maximum concurrent worker threads. Use <c>null</c> for unlimited.
    /// Recommended: 2-4x CPU cores.
    /// </param>
    /// <returns>The current guide instance for method chaining.</returns>
    public ModuleJobSchedulerGuide SetMaxWorkerExecutionThreads(int? maxThreads)
    {
        return ConfigureModuleOption(options =>
        {
            options.MaxWorkerExecutionThreads = maxThreads;
        });
    }
}
