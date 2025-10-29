using MoLibrary.Core.Module.Interfaces;
using MoLibrary.TaskScheduler.Abstractions;

namespace MoLibrary.TaskScheduler.Modules;

/// <summary>
/// Fluent configuration builder for the Task Scheduler module.
/// Provides chainable methods for configuring task scheduler options and dependencies.
/// </summary>
/// <remarks>
/// <para>
/// ModuleTaskSchedulerGuide enables fluent configuration of the task scheduler module
/// using a builder pattern. This provides a more readable and intuitive API compared to
/// direct option configuration.
/// </para>
/// <example>
/// <para><b>Basic configuration with fluent API:</b></para>
/// <code>
/// builder.ConfigMoTaskScheduler()
///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;()
///     .ConfigureModuleOption(options =>
///     {
///         options.RecurringTaskDebugMode = true;
///         options.MaxWorkerExecutionThreads = 10;
///     });
/// </code>
/// </example>
/// </remarks>
public class ModuleTaskSchedulerGuide
    : MoModuleGuide<ModuleTaskScheduler, ModuleTaskSchedulerOption, ModuleTaskSchedulerGuide>
{
    /// <summary>
    /// Configures a custom metadata store implementation for task persistence.
    /// The specified type will be registered as the IMoTaskScheduleMetadataStore implementation.
    /// </summary>
    /// <typeparam name="TStore">
    /// The custom metadata store type. Must implement <see cref="IMoTaskScheduleMetadataStore"/>.
    /// </typeparam>
    /// <returns>
    /// The current guide instance for method chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method sets the CustomMetadataStoreType property in the module options.
    /// The specified type will be registered as a singleton when the module is initialized.
    /// </para>
    /// <para>
    /// If not called, the module will use the default MetadataStoreInMemoryProvider.
    /// </para>
    /// <para>
    /// <b>Requirements for custom metadata stores:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Must implement IMoTaskScheduleMetadataStore interface</description></item>
    /// <item><description>Must have a public constructor (preferably with dependency injection support)</description></item>
    /// <item><description>Should be thread-safe for concurrent access from multiple workers</description></item>
    /// <item><description>Should provide atomic operations for state transitions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para><b>Using a SQL Server-based metadata store:</b></para>
    /// <code>
    /// builder.ConfigMoTaskScheduler()
    ///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;()
    ///     .ConfigureModuleOption(options =>
    ///     {
    ///         options.MaxWorkerExecutionThreads = 10;
    ///     });
    /// </code>
    ///
    /// <para><b>Using MongoDB for metadata persistence:</b></para>
    /// <code>
    /// builder.ConfigMoTaskScheduler()
    ///     .UseCustomMetadataStore&lt;MongoDbMetadataStore&gt;()
    ///     .ConfigureModuleOption(options =>
    ///     {
    ///         options.RecurringTaskDebugMode = false;
    ///         options.TriggeredTaskDebugMode = false;
    ///     });
    /// </code>
    /// </example>
    public ModuleTaskSchedulerGuide UseCustomMetadataStore<TStore>()
        where TStore : IMoTaskScheduleMetadataStore
    {
        return ConfigureModuleOption(options =>
        {
            options.CustomMetadataStoreType = typeof(TStore);
        });
    }

    /// <summary>
    /// Enables recurring task debug mode, preventing automatic cron-based scheduling.
    /// Tasks can still be triggered manually via the API for testing.
    /// </summary>
    /// <param name="enabled">
    /// <c>true</c> to enable debug mode; <c>false</c> to disable. Default is <c>true</c>.
    /// </param>
    /// <returns>
    /// The current guide instance for method chaining.
    /// </returns>
    /// <remarks>
    /// When enabled, recurring tasks will not be automatically scheduled based on their
    /// cron expressions. This is useful during development and testing when you want
    /// manual control over task execution.
    /// </remarks>
    public ModuleTaskSchedulerGuide EnableRecurringTaskDebugMode(bool enabled = true)
    {
        return ConfigureModuleOption(options =>
        {
            options.RecurringTaskDebugMode = enabled;
        });
    }

    /// <summary>
    /// Enables triggered task debug mode, preventing automatic execution after EnqueueAsync.
    /// Task instances are created but not published to the event bus.
    /// </summary>
    /// <param name="enabled">
    /// <c>true</c> to enable debug mode; <c>false</c> to disable. Default is <c>true</c>.
    /// </param>
    /// <returns>
    /// The current guide instance for method chaining.
    /// </returns>
    /// <remarks>
    /// When enabled, triggered tasks will be created in the metadata store but not
    /// automatically published for execution. This is useful during development and
    /// testing when you want to inspect task instances without actual execution.
    /// </remarks>
    public ModuleTaskSchedulerGuide EnableTriggeredTaskDebugMode(bool enabled = true)
    {
        return ConfigureModuleOption(options =>
        {
            options.TriggeredTaskDebugMode = enabled;
        });
    }

    /// <summary>
    /// Sets the maximum number of concurrent task executions allowed on this worker instance.
    /// </summary>
    /// <param name="maxThreads">
    /// The maximum number of concurrent worker execution threads.
    /// Use <c>null</c> for unlimited concurrent executions.
    /// </param>
    /// <returns>
    /// The current guide instance for method chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This setting controls the worker-level thread limit, different from per-task
    /// concurrency limits defined in TaskConfigAttribute.
    /// </para>
    /// <para>
    /// A good starting point is 2-4 times the number of CPU cores. Setting this too low
    /// may cause task queuing and increased latency. Setting this too high may cause
    /// resource contention.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.ConfigMoTaskScheduler()
    ///     .SetMaxWorkerExecutionThreads(10);
    /// </code>
    /// </example>
    public ModuleTaskSchedulerGuide SetMaxWorkerExecutionThreads(int? maxThreads)
    {
        return ConfigureModuleOption(options =>
        {
            options.MaxWorkerExecutionThreads = maxThreads;
        });
    }
}
