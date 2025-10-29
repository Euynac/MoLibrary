using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.TaskScheduler.Modules;

/// <summary>
/// Configuration options for the Task Scheduler module.
/// Provides settings for debug modes, worker thread limits, and custom metadata store registration.
/// </summary>
/// <remarks>
/// <para>
/// ModuleTaskSchedulerOption controls the behavior of the task scheduler system including:
/// </para>
/// <list type="bullet">
/// <item><description>Debug modes for development and testing scenarios</description></item>
/// <item><description>Worker thread limits for resource management</description></item>
/// <item><description>Custom metadata store implementations for different persistence backends</description></item>
/// </list>
/// </remarks>
public class ModuleTaskSchedulerOption : MoModuleOption<ModuleTaskScheduler>
{
    /// <summary>
    /// Gets or sets a value indicating whether recurring task debug mode is enabled.
    /// When enabled, recurring tasks will not be automatically scheduled based on cron expressions.
    /// Tasks can still be triggered manually via the API for testing purposes.
    /// </summary>
    /// <value>
    /// <c>true</c> to disable automatic recurring task scheduling; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This mode is useful during development and testing when you want to control task execution
    /// manually without cron-based automatic scheduling interfering with your tests.
    /// </remarks>
    public bool RecurringTaskDebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether triggered task debug mode is enabled.
    /// When enabled, triggered tasks will be created but not automatically published to the event bus.
    /// This allows inspection of task instances without actual execution.
    /// </summary>
    /// <value>
    /// <c>true</c> to disable automatic triggered task execution; otherwise, <c>false</c>.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This mode is useful during development and testing when you want to create task instances
    /// (e.g., via EnqueueAsync) but delay or prevent their automatic execution. Tasks can still
    /// be triggered manually via the API.
    /// </remarks>
    public bool TriggeredTaskDebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of concurrent task executions allowed on this worker instance.
    /// When set to <c>null</c>, there is no limit on concurrent executions (unlimited).
    /// When set to a positive integer, the worker will limit concurrent task executions to that number.
    /// </summary>
    /// <value>
    /// The maximum number of concurrent worker execution threads, or <c>null</c> for unlimited.
    /// Default is <c>null</c> (unlimited).
    /// </value>
    /// <remarks>
    /// <para>
    /// This setting controls the worker-level thread limit. It is different from per-task concurrency
    /// limits (MaxConcurrency in TaskConfigAttribute), which control how many instances of a specific
    /// task can run concurrently.
    /// </para>
    /// <para>
    /// Use this setting to prevent resource exhaustion when running many different task types.
    /// For example, setting this to 10 means the worker will execute at most 10 tasks concurrently,
    /// regardless of task type.
    /// </para>
    /// <para>
    /// <b>Performance Consideration:</b> Setting this too low may cause task queuing and increased
    /// latency. Setting this too high may cause resource contention. A good starting point is
    /// 2-4 times the number of CPU cores.
    /// </para>
    /// </remarks>
    public int? MaxWorkerExecutionThreads { get; set; } = null;

    /// <summary>
    /// Gets or sets the custom metadata store implementation type.
    /// When set, the module will register the specified type as the IMoTaskScheduleMetadataStore implementation.
    /// When <c>null</c>, the default MetadataStoreInMemoryProvider will be used.
    /// </summary>
    /// <value>
    /// The <see cref="Type"/> of the custom metadata store implementation, or <c>null</c> for the default.
    /// Default is <c>null</c> (uses MetadataStoreInMemoryProvider).
    /// </value>
    /// <remarks>
    /// <para>
    /// The custom metadata store type must implement <see cref="Abstractions.IMoTaskScheduleMetadataStore"/>.
    /// The type will be registered as a singleton in the dependency injection container.
    /// </para>
    /// <para>
    /// Common scenarios for custom metadata stores:
    /// </para>
    /// <list type="bullet">
    /// <item><description>SQL database persistence (SQL Server, PostgreSQL, MySQL)</description></item>
    /// <item><description>NoSQL database persistence (MongoDB, Redis)</description></item>
    /// <item><description>Distributed cache integration for multi-worker scenarios</description></item>
    /// </list>
    /// <example>
    /// <para><b>Registering a custom SQL-based metadata store:</b></para>
    /// <code>
    /// builder.ConfigMoTaskScheduler(options =>
    /// {
    ///     options.CustomMetadataStoreType = typeof(SqlServerMetadataStore);
    /// });
    ///
    /// // Or using fluent API:
    /// builder.ConfigMoTaskScheduler()
    ///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;();
    /// </code>
    /// </example>
    /// </remarks>
    public Type? CustomMetadataStoreType { get; set; } = null;
}
