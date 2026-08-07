using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Modules;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Defines an immutable Configuration input declaration that is projected consistently into bootstrap configuration,
/// startup effective-options loading, and runtime module composition.
/// </summary>
/// <remarks>
/// Custom store compositions and captured callbacks must project the same logical store in every phase. Built-in file
/// store declarations snapshot their scalar settings immediately.
/// </remarks>
public sealed class MonicaConfigurationInputPlan
{
    private readonly IMonicaConfigurationStoreComposition _storeComposition;
    private readonly ImmutableArray<ManagedJsonConfigurationSourceRegistration> _managedJsonSources;

    internal MonicaConfigurationInputPlan(
        IMonicaConfigurationStoreComposition storeComposition,
        ImmutableArray<ManagedJsonConfigurationSourceRegistration> managedJsonSources,
        ConfigurationSectionPathConvention sectionPathConvention)
    {
        _storeComposition = storeComposition;
        _managedJsonSources = managedJsonSources;
        SectionPathConvention = sectionPathConvention;
    }

    internal ConfigurationSectionPathConvention SectionPathConvention { get; }

    /// <summary>
    /// Creates an immutable input plan from one bounded declaration callback.
    /// </summary>
    /// <param name="configure">Declares exactly one store and any ordered managed JSON sources.</param>
    /// <returns>The immutable input plan.</returns>
    /// <remarks>
    /// The callback builder is sealed when this method returns. Retaining it and attempting later mutation throws.
    /// Plan creation performs no file, database, dependency-injection, or host mutation work.
    /// </remarks>
    public static MonicaConfigurationInputPlan Create(
        Action<MonicaConfigurationInputPlanBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MonicaConfigurationInputPlanBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Builds bootstrap configuration from the host's current configuration followed by the plan's managed JSON sources.
    /// </summary>
    /// <param name="hostBuilder">The host builder that supplies the base configuration and content root.</param>
    /// <returns>A caller-owned configuration root.</returns>
    /// <remarks>
    /// Later managed JSON declarations have higher priority. The returned root owns any reload registrations it creates
    /// and should be disposed after bootstrap and effective-options loading complete.
    /// </remarks>
    public MonicaBootstrapConfiguration BuildBootstrapConfiguration(IHostApplicationBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var builder = CreateConfigurationBuilder(hostBuilder);
        builder.AddConfiguration(hostBuilder.Configuration);
        AddManagedJsonSources(builder);
        return new MonicaBootstrapConfiguration(
            this,
            hostBuilder.Configuration,
            hostBuilder.Environment.ContentRootPath,
            builder.Build());
    }

    /// <summary>
    /// Ensures and loads several effective options types in one synchronous operation.
    /// </summary>
    /// <param name="bootstrapConfiguration">The bootstrap configuration created by this plan.</param>
    /// <param name="optionsTypes">The annotated options types to ensure and load.</param>
    /// <param name="configure">Optional startup diagnostics configuration.</param>
    /// <param name="logger">Optional startup diagnostic logger.</param>
    /// <returns>The loaded effective-options snapshot.</returns>
    /// <remarks>
    /// This operation may create missing effective-value documents. It disposes the isolated startup store before
    /// returning. Use <see cref="EnsureEffectiveOptionsSnapshotAsync"/> when the surrounding startup flow is asynchronous.
    /// </remarks>
    public MonicaEffectiveOptionsSnapshot EnsureEffectiveOptionsSnapshot(
        MonicaBootstrapConfiguration bootstrapConfiguration,
        IReadOnlyCollection<Type> optionsTypes,
        Action<MonicaEffectiveOptionsSnapshotOptions>? configure = null,
        ILogger? logger = null)
    {
        ValidateSnapshotRequest(bootstrapConfiguration, optionsTypes);

        using var loader = CreateSnapshotLoader(
            bootstrapConfiguration,
            configure,
            logger);
        return loader.Load(optionsTypes);
    }

    /// <summary>
    /// Ensures and loads several effective options types in one asynchronous operation.
    /// </summary>
    /// <param name="bootstrapConfiguration">The bootstrap configuration created by this plan.</param>
    /// <param name="optionsTypes">The annotated options types to ensure and load.</param>
    /// <param name="configure">Optional startup diagnostics configuration.</param>
    /// <param name="logger">Optional startup diagnostic logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded effective-options snapshot.</returns>
    /// <remarks>
    /// This operation may create missing effective-value documents. It asynchronously disposes the isolated startup
    /// store on success, cancellation, or failure.
    /// </remarks>
    public async Task<MonicaEffectiveOptionsSnapshot> EnsureEffectiveOptionsSnapshotAsync(
        MonicaBootstrapConfiguration bootstrapConfiguration,
        IReadOnlyCollection<Type> optionsTypes,
        Action<MonicaEffectiveOptionsSnapshotOptions>? configure = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSnapshotRequest(bootstrapConfiguration, optionsTypes);

        await using var loader = CreateSnapshotLoader(
            bootstrapConfiguration,
            configure,
            logger);
        return await loader.LoadAsync(optionsTypes, cancellationToken);
    }

    internal void ConfigureRuntime(
        ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _storeComposition.ConfigureRuntime(module);
        foreach (var source in _managedJsonSources)
        {
            module.ConfigureBuilder(context =>
            {
                context.HostApplicationBuilder.Configuration.AddJsonFile(
                    source.Path,
                    source.Optional,
                    source.ReloadOnChange);
                ManagedJsonConfigurationSourceRegistry.Add(
                    context.HostApplicationBuilder.Configuration,
                    source);
            }, ModuleRegistrationOrder.AfterModule);
        }
    }

    private MonicaEffectiveOptionsSnapshotLoader CreateSnapshotLoader(
        MonicaBootstrapConfiguration bootstrapConfiguration,
        Action<MonicaEffectiveOptionsSnapshotOptions>? configure,
        ILogger? logger)
    {
        var options = new MonicaEffectiveOptionsSnapshotOptions();
        configure?.Invoke(options);
        var store = _storeComposition.CreateStartupStore()
            ?? throw new InvalidOperationException(
                $"Configuration store composition '{_storeComposition.Name}' returned no startup store.");

        try
        {
            return new MonicaEffectiveOptionsSnapshotLoader(
                bootstrapConfiguration,
                SectionPathConvention,
                options,
                store,
                _managedJsonSources,
                logger ?? NullLogger.Instance);
        }
        catch
        {
            DisposeStore(store);
            throw;
        }
    }

    private static ConfigurationBuilder CreateConfigurationBuilder(IHostApplicationBuilder hostBuilder)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(hostBuilder.Environment.ContentRootPath))
        {
            builder.SetBasePath(hostBuilder.Environment.ContentRootPath);
        }

        return builder;
    }

    private void AddManagedJsonSources(IConfigurationBuilder builder)
    {
        foreach (var source in _managedJsonSources)
        {
            builder.AddJsonFile(source.Path, source.Optional, source.ReloadOnChange);
        }
    }

    private void ValidateSnapshotRequest(
        MonicaBootstrapConfiguration bootstrapConfiguration,
        IReadOnlyCollection<Type> optionsTypes)
    {
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);
        ArgumentNullException.ThrowIfNull(optionsTypes);
        bootstrapConfiguration.EnsureOwnedBy(this);
        if (optionsTypes.Count == 0)
        {
            throw new ArgumentException("At least one effective options type is required.", nameof(optionsTypes));
        }
    }

    private static void DisposeStore(IConfigurationEffectiveValueStore store)
    {
        if (store is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (store is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
