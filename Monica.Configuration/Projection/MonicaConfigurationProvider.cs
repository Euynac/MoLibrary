using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Projection;

/// <summary>
/// Single Microsoft.Extensions.Configuration provider that exposes Monica effective value documents.
/// </summary>
internal sealed class MonicaConfigurationProvider(MonicaConfigurationProviderAccessor accessor) : ConfigurationProvider
{
    private readonly Lock _projectionLock = new();
    private readonly Dictionary<string, IReadOnlyCollection<string>> _projectedKeysByDefinitionKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long?> _loadedVersionsByDefinitionKey = new(StringComparer.OrdinalIgnoreCase);
    private long _reloadCount;
    private long _failedReloadCount;
    private DateTimeOffset? _lastReloadedAt;
    private DateTimeOffset? _lastFailedAt;
    private TimeSpan? _lastReloadDuration;
    private string? _lastFailureMessage;

    /// <inheritdoc />
    public override void Load()
    {
        ReloadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reloads effective value documents and emits a new flat configuration projection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (accessor.ServiceProvider is null)
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        var definitionRegistry = accessor.ServiceProvider.GetRequiredService<IConfigurationDefinitionRegistry>();
        var effectiveValueStore = accessor.ServiceProvider.GetRequiredService<IConfigurationEffectiveValueStore>();
        var seedFactory = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueSeedFactory>();
        var documentEditor = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueDocumentEditor>();
        var metricsRecorder = accessor.ServiceProvider.GetRequiredService<ConfigurationMetricsRecorder>();
        var stateTracker = accessor.ServiceProvider.GetRequiredService<IConfigurationStoreStateTracker>();

        try
        {
            var projected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var definitions = definitionRegistry.GetAll();
            var seeds = definitions
                .Select(definition => new ConfigurationEffectiveValueSeed
                {
                    Definition = definition,
                    SeedJson = seedFactory.CreateSeedJson(definition)
                })
                .ToArray();
            var documents = await effectiveValueStore.EnsureCreatedAsync(seeds, cancellationToken);

            var projectedKeysByDefinitionKey = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
            var loadedVersionsByDefinitionKey = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (definition, document) in definitions.Zip(documents))
            {
                var definitionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in documentEditor.Project(definition, document.Json))
                {
                    projected[key] = value;
                    definitionKeys.Add(key);
                }

                projectedKeysByDefinitionKey[definition.DefinitionKey] = definitionKeys;
                loadedVersionsByDefinitionKey[definition.DefinitionKey] = document.Version;
            }

            lock (_projectionLock)
            {
                Data = projected;
                _projectedKeysByDefinitionKey.Clear();
                foreach (var (definitionKey, keys) in projectedKeysByDefinitionKey)
                {
                    _projectedKeysByDefinitionKey[definitionKey] = keys;
                }

                _loadedVersionsByDefinitionKey.Clear();
                foreach (var (definitionKey, version) in loadedVersionsByDefinitionKey)
                {
                    _loadedVersionsByDefinitionKey[definitionKey] = version;
                }
            }

            stateTracker.RecordSuccess(effectiveValueStore.Descriptor.StoreKey);
            var elapsed = TimeProvider.System.GetElapsedTime(started);
            RecordReloadSuccess(elapsed);
            metricsRecorder.RecordReloadLatency(elapsed);
            OnReload();
        }
        catch (Exception ex)
        {
            RecordReloadFailure(ex);
            stateTracker.RecordFailure(effectiveValueStore.Descriptor.StoreKey, ex);
            throw;
        }
    }

    /// <summary>
    /// Reloads one effective-value definition into the flat configuration projection.
    /// </summary>
    /// <param name="definitionKey">The definition key to reload.</param>
    /// <param name="minimumVersion">Optional minimum version already observed in a reload signal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReloadDefinitionAsync(string definitionKey, long? minimumVersion, CancellationToken cancellationToken)
    {
        if (accessor.ServiceProvider is null)
        {
            return;
        }

        if (minimumVersion is not null && GetLoadedVersion(definitionKey) >= minimumVersion)
        {
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        var definitionRegistry = accessor.ServiceProvider.GetRequiredService<IConfigurationDefinitionRegistry>();
        if (!definitionRegistry.TryGet(definitionKey, out var definition))
        {
            return;
        }

        var effectiveValueStore = accessor.ServiceProvider.GetRequiredService<IConfigurationEffectiveValueStore>();
        var seedFactory = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueSeedFactory>();
        var documentEditor = accessor.ServiceProvider.GetRequiredService<ConfigurationEffectiveValueDocumentEditor>();
        var metricsRecorder = accessor.ServiceProvider.GetRequiredService<ConfigurationMetricsRecorder>();
        var stateTracker = accessor.ServiceProvider.GetRequiredService<IConfigurationStoreStateTracker>();

        try
        {
            var resolvedDefinition = definition!;
            var seedJson = seedFactory.CreateSeedJson(resolvedDefinition);
            var document = await effectiveValueStore.EnsureCreatedAsync(resolvedDefinition, seedJson, cancellationToken);
            var loadedVersion = GetLoadedVersion(definitionKey);
            if (loadedVersion is not null && document.Version <= loadedVersion)
            {
                return;
            }

            var projected = documentEditor.Project(resolvedDefinition, document.Json);
            lock (_projectionLock)
            {
                var nextData = new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase);
                if (_projectedKeysByDefinitionKey.TryGetValue(resolvedDefinition.DefinitionKey, out var oldKeys))
                {
                    foreach (var key in oldKeys)
                    {
                        nextData.Remove(key);
                    }
                }

                foreach (var (key, value) in projected)
                {
                    nextData[key] = value;
                }

                Data = nextData;
                _projectedKeysByDefinitionKey[resolvedDefinition.DefinitionKey] = projected.Keys.ToArray();
                _loadedVersionsByDefinitionKey[resolvedDefinition.DefinitionKey] = document.Version;
            }

            stateTracker.RecordSuccess(effectiveValueStore.Descriptor.StoreKey);
            var elapsed = TimeProvider.System.GetElapsedTime(started);
            RecordReloadSuccess(elapsed);
            metricsRecorder.RecordReloadLatency(elapsed);
            OnReload();
        }
        catch (Exception ex)
        {
            RecordReloadFailure(ex);
            stateTracker.RecordFailure(effectiveValueStore.Descriptor.StoreKey, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the loaded effective-value document version for one definition.
    /// </summary>
    /// <param name="definitionKey">Definition key.</param>
    /// <returns>The loaded version, or null when the definition has not been loaded.</returns>
    public long? GetLoadedVersion(string definitionKey)
    {
        lock (_projectionLock)
        {
            return _loadedVersionsByDefinitionKey.GetValueOrDefault(definitionKey);
        }
    }

    /// <summary>
    /// Gets the current reload state for this provider.
    /// </summary>
    /// <returns>The reload state.</returns>
    public ConfigurationRuntimeReloadState GetReloadState()
    {
        lock (_projectionLock)
        {
            return new ConfigurationRuntimeReloadState
            {
                IsProviderActive = accessor.ServiceProvider is not null,
                ReloadCount = _reloadCount,
                LastReloadedAt = _lastReloadedAt,
                LastReloadDuration = _lastReloadDuration,
                FailedReloadCount = _failedReloadCount,
                LastFailedAt = _lastFailedAt,
                LastFailureMessage = _lastFailureMessage
            };
        }
    }

    private void RecordReloadSuccess(TimeSpan elapsed)
    {
        lock (_projectionLock)
        {
            _reloadCount++;
            _lastReloadedAt = DateTimeOffset.UtcNow;
            _lastReloadDuration = elapsed;
            _lastFailureMessage = null;
        }
    }

    private void RecordReloadFailure(Exception exception)
    {
        lock (_projectionLock)
        {
            _failedReloadCount++;
            _lastFailedAt = DateTimeOffset.UtcNow;
            _lastFailureMessage = exception.Message;
        }
    }
}
