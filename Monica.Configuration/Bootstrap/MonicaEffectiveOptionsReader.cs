using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Binding;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Monica.Core.Logging;

namespace Monica.Configuration.Bootstrap;

internal sealed class MonicaEffectiveOptionsReader : IMonicaEffectiveOptionsReader
{
    private static readonly ILogger Logger = LogManager.For(typeof(MonicaEffectiveOptionsReader));

    private readonly IConfigurationEffectiveValueStore _store;
    private readonly MonicaEffectiveOptionsReaderOptions _options;
    private readonly ConfigurationDefinitionScanner _definitionScanner;
    private readonly ConfigurationEffectiveValueSeedFactory _seedFactory;
    private readonly ConfigurationEffectiveValueDocumentEditor _documentEditor;
    private readonly Dictionary<Type, object> _loadedOptions = new();
    private readonly object _cacheLock = new();
    private bool _disposed;

    public MonicaEffectiveOptionsReader(
        IConfiguration bootstrapConfiguration,
        MonicaEffectiveOptionsReaderOptions options,
        IConfigurationEffectiveValueStore store)
    {
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _options = options;
        _definitionScanner = new ConfigurationDefinitionScanner(
            new ConfigurationSchemaHasher(),
            options.SectionPathConvention);

        var runtimeContext = new ConfigurationRuntimeContext();
        runtimeContext.Capture(bootstrapConfiguration);
        _seedFactory = new ConfigurationEffectiveValueSeedFactory(runtimeContext);
        _documentEditor = new ConfigurationEffectiveValueDocumentEditor(
            new ConfigurationEffectiveValuePatchEngine(),
            new ConfigurationStoredValueCodec());
    }

    public TOptions Get<TOptions>()
        where TOptions : class, new()
    {
        return GetMany(typeof(TOptions)).Get<TOptions>();
    }

    public async Task<TOptions> GetAsync<TOptions>(CancellationToken cancellationToken = default)
        where TOptions : class, new()
    {
        return (await GetManyAsync([typeof(TOptions)], cancellationToken)).Get<TOptions>();
    }

    public MonicaEffectiveOptionsSnapshot GetMany(params Type[] optionsTypes)
    {
        return GetManyAsync(optionsTypes, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<MonicaEffectiveOptionsSnapshot> GetManyAsync(
        IReadOnlyCollection<Type> optionsTypes,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(optionsTypes);

        var requestedTypes = NormalizeRequestedTypes(optionsTypes);
        var missingTypes = FindMissingTypes(requestedTypes);
        if (missingTypes.Count > 0)
        {
            await LoadMissingTypesAsync(missingTypes, cancellationToken);
        }

        return CreateSnapshot(requestedTypes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_store is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_store is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (_store is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static IReadOnlyList<Type> NormalizeRequestedTypes(IEnumerable<Type> optionsTypes)
    {
        var result = new List<Type>();
        var seen = new HashSet<Type>();
        foreach (var optionsType in optionsTypes)
        {
            ArgumentNullException.ThrowIfNull(optionsType);
            if (!seen.Add(optionsType))
            {
                continue;
            }

            result.Add(optionsType);
        }

        return result;
    }

    private IReadOnlyList<Type> FindMissingTypes(IReadOnlyList<Type> requestedTypes)
    {
        lock (_cacheLock)
        {
            return requestedTypes
                .Where(type => !_loadedOptions.ContainsKey(type))
                .ToArray();
        }
    }

    private async Task LoadMissingTypesAsync(IReadOnlyList<Type> missingTypes, CancellationToken cancellationToken)
    {
        var definitions = missingTypes
            .Select(ScanOptionsType)
            .ToArray();

        var seeds = definitions
            .Select(definition => new ConfigurationEffectiveValueSeed
            {
                Definition = definition,
                SeedJson = _seedFactory.CreateSeedJson(definition)
            })
            .ToArray();

        if (_options.Debugging)
        {
            Logger.LogInformation(
                "Reading {OptionsCount} Monica effective options from store '{StoreKey}'.",
                seeds.Length,
                _store.Descriptor.StoreKey);
        }

        var documents = await _store.EnsureCreatedAsync(seeds, cancellationToken);
        if (documents.Count != definitions.Length)
        {
            throw new InvalidOperationException(
                $"The Monica effective-value store returned {documents.Count} documents for {definitions.Length} requested definitions.");
        }

        var loaded = new Dictionary<Type, object>();
        for (var i = 0; i < definitions.Length; i++)
        {
            loaded[missingTypes[i]] = BindOptions(missingTypes[i], definitions[i], documents[i]);
        }

        lock (_cacheLock)
        {
            foreach (var (optionsType, options) in loaded)
            {
                _loadedOptions.TryAdd(optionsType, options);
            }
        }
    }

    private ConfigurationDefinition ScanOptionsType(Type optionsType)
    {
        if (optionsType.GetCustomAttribute<ConfigurationAttribute>(inherit: false) is null)
        {
            throw new InvalidOperationException(
                $"Options type '{optionsType.FullName ?? optionsType.Name}' cannot be read from Monica effective values because it is not marked with {nameof(ConfigurationAttribute)}.");
        }

        try
        {
            return _definitionScanner.Scan(optionsType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to scan Monica effective options type '{optionsType.FullName ?? optionsType.Name}'.",
                ex);
        }
    }

    private object BindOptions(
        Type optionsType,
        ConfigurationDefinition definition,
        ConfigurationEffectiveValueDocument document)
    {
        if (!string.Equals(definition.DefinitionKey, document.DefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Monica effective-value store returned document '{document.DefinitionKey}' for requested definition '{definition.DefinitionKey}'.");
        }

        try
        {
            var values = _documentEditor.Project(definition, document.Json);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var instance = Activator.CreateInstance(optionsType, nonPublic: true)
                ?? throw new InvalidOperationException(
                    $"Failed to create Monica effective options type '{optionsType.FullName ?? optionsType.Name}'. A parameterless constructor is required.");

            MonicaConfigurationBinder.Bind(configuration.GetSection(definition.SectionPath), instance);
            return instance;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to bind Monica effective options type '{optionsType.FullName ?? optionsType.Name}' from section '{definition.SectionPath}'.",
                ex);
        }
    }

    private MonicaEffectiveOptionsSnapshot CreateSnapshot(IReadOnlyList<Type> requestedTypes)
    {
        var snapshot = new Dictionary<Type, object>();
        lock (_cacheLock)
        {
            foreach (var optionsType in requestedTypes)
            {
                snapshot[optionsType] = _loadedOptions[optionsType];
            }
        }

        return new MonicaEffectiveOptionsSnapshot(snapshot);
    }
}
