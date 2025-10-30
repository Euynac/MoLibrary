using Microsoft.AspNetCore.Builder;

namespace MoLibrary.JobScheduler.Modules;

/// <summary>
/// Extension methods for <see cref="WebApplicationBuilder"/> to configure the Job Scheduler module.
/// Provides convenient methods for registering and configuring the job scheduler in ASP.NET Core applications.
/// </summary>
public static class ModuleJobSchedulerBuilderExtensions
{
    /// <summary>
    /// Configures the Job Scheduler module for the application.
    /// Registers all required services, hosted services, and job definitions.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <param name="configure">
    /// An optional action to configure <see cref="ModuleJobSchedulerOption"/>.
    /// Use this for lambda-based configuration of module options.
    /// </param>
    /// <returns>
    /// A <see cref="ModuleJobSchedulerGuide"/> instance for fluent configuration.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method initializes the job scheduler module and provides two configuration styles:
    /// </para>
    /// <list type="number">
    /// <item><description><b>Lambda configuration:</b> Pass an <see cref="Action{ModuleJobSchedulerOption}"/> to configure options directly</description></item>
    /// <item><description><b>Fluent configuration:</b> Chain methods on the returned <see cref="ModuleJobSchedulerGuide"/> for a builder-style API</description></item>
    /// </list>
    /// <para>
    /// The module will automatically:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Scan for RecurringJob and TriggeredJob classes in the application</description></item>
    /// <item><description>Register job definitions to the metadata store</description></item>
    /// <item><description>Start the JobScheduler for cron-based scheduling</description></item>
    /// <item><description>Start the JobWorkerManager for job execution</description></item>
    /// <item><description>Register metadata store (default or custom)</description></item>
    /// <item><description>Register all required services and dependencies</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para><b>Lambda-based configuration:</b></para>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.ConfigMoJobScheduler(options =>
    /// {
    ///     options.RecurringJobDebugMode = true;
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
    /// builder.ConfigMoJobScheduler()
    ///     .UseCustomMetadataStore&lt;SqlServerMetadataStore&gt;()
    ///     .EnableRecurringJobDebugMode()
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
    /// builder.ConfigMoJobScheduler(options =>
    ///     {
    ///         // Configure some options via lambda
    ///         options.RecurringJobDebugMode = true;
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
    /// builder.ConfigMoJobScheduler();
    ///
    /// var app = builder.Build();
    /// app.Run();
    /// </code>
    /// </example>
    public static ModuleJobSchedulerGuide ConfigMoJobScheduler(
        this WebApplicationBuilder builder,
        Action<ModuleJobSchedulerOption>? configure = null)
    {
        // Create the guide instance
        var guide = new ModuleJobSchedulerGuide();

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
