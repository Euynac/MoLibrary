using Microsoft.AspNetCore.Builder;

namespace MoLibrary.TaskScheduler.Modules;

/// <summary>
/// Extension methods for <see cref="WebApplicationBuilder"/> to configure the Task Scheduler module.
/// Provides convenient methods for registering and configuring the task scheduler in ASP.NET Core applications.
/// </summary>
public static class ModuleTaskSchedulerBuilderExtensions
{
    /// <summary>
    /// Configures the Task Scheduler module for the application.
    /// Registers all required services, hosted services, and task definitions.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="configure">
    /// An optional action to configure <see cref="ModuleTaskSchedulerOption"/>.
    /// Use this for lambda-based configuration of module options.
    /// </param>
    /// <returns>
    /// A <see cref="ModuleTaskSchedulerGuide"/> instance for fluent configuration.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method initializes the task scheduler module and provides two configuration styles:
    /// </para>
    /// <list type="number">
    /// <item><description><b>Lambda configuration:</b> Pass an <see cref="Action{ModuleTaskSchedulerOption}"/> to configure options directly</description></item>
    /// <item><description><b>Fluent configuration:</b> Chain methods on the returned <see cref="ModuleTaskSchedulerGuide"/> for a builder-style API</description></item>
    /// </list>
    /// <para>
    /// The module will automatically:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Scan for RecurringTask and TriggeredTask classes in the application</description></item>
    /// <item><description>Register task definitions to the metadata store</description></item>
    /// <item><description>Start the TaskScheduler for cron-based scheduling</description></item>
    /// <item><description>Start the TaskWorkerManager for task execution</description></item>
    /// <item><description>Register metadata store (default or custom)</description></item>
    /// <item><description>Register all required services and dependencies</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para><b>Lambda-based configuration:</b></para>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.ConfigMoTaskScheduler(options =>
    /// {
    ///     options.RecurringTaskDebugMode = true;
    ///     options.MaxWorkerExecutionThreads = 10;
    ///     options.CustomMetadataStoreType = typeof(SqlServerMetadataStore);
    /// });
    ///
    /// var app = builder.Build();
    /// app.Run();
    /// </code>
    ///
    /// <para><b>Fluent configuration style:</b></para>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.ConfigMoTaskScheduler()
    ///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;()
    ///     .EnableRecurringTaskDebugMode()
    ///     .SetMaxWorkerExecutionThreads(10);
    ///
    /// var app = builder.Build();
    /// app.Run();
    /// </code>
    ///
    /// <para><b>Combined configuration (lambda + fluent):</b></para>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.ConfigMoTaskScheduler(options =>
    ///     {
    ///         // Configure some options via lambda
    ///         options.RecurringTaskDebugMode = true;
    ///     })
    ///     // Chain additional configuration fluently
    ///     .SetMaxWorkerExecutionThreads(10)
    ///     .UseCustomMetadataStore&lt;MongoDbMetadataStore&gt;();
    ///
    /// var app = builder.Build();
    /// app.Run();
    /// </code>
    ///
    /// <para><b>Minimal configuration (use defaults):</b></para>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// // Register with all defaults:
    /// // - In-memory metadata store
    /// // - Debug modes disabled
    /// // - Unlimited worker threads
    /// builder.ConfigMoTaskScheduler();
    ///
    /// var app = builder.Build();
    /// app.Run();
    /// </code>
    /// </example>
    public static ModuleTaskSchedulerGuide ConfigMoTaskScheduler(
        this WebApplicationBuilder builder,
        Action<ModuleTaskSchedulerOption>? configure = null)
    {
        // Create the guide instance
        var guide = new ModuleTaskSchedulerGuide();

        // If lambda configuration is provided, apply it
        if (configure != null)
        {
            guide.ConfigureModuleOption(configure);
        }

        // Register the module
        guide.Register();

        // Return guide for fluent chaining
        return guide;
    }
}
