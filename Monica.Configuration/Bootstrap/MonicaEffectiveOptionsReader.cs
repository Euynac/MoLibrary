using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Binding;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Bootstrap;

internal sealed class MonicaEffectiveOptionsReader : IMonicaEffectiveOptionsReader
{
    private readonly IConfigurationEffectiveValueStore _store;
    private readonly IConfiguration _bootstrapConfiguration;
    private readonly string _contentRootPath;
    private readonly IReadOnlyList<ManagedJsonConfigurationSourceRegistration> _managedJsonSources;
    private readonly IConfigurationRoot _seedConfiguration;
    private readonly MonicaEffectiveOptionsReaderOptions _options;
    private readonly ConfigurationDefinitionScanner _definitionScanner;
    private readonly ConfigurationEffectiveValueSeedFactory _seedFactory;
    private readonly ConfigurationEffectiveValueDocumentEditor _documentEditor;
    private readonly ILogger _logger;
    private readonly Dictionary<Type, object> _loadedOptions = new();
    private readonly object _cacheLock = new();
    private bool _disposed;

    public MonicaEffectiveOptionsReader(
        IHostApplicationBuilder hostBuilder,
        IConfiguration bootstrapConfiguration,
        MonicaEffectiveOptionsReaderOptions options,
        IConfigurationEffectiveValueStore store,
        IReadOnlyList<ManagedJsonConfigurationSourceRegistration> managedJsonSources,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(managedJsonSources);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _bootstrapConfiguration = bootstrapConfiguration;
        _contentRootPath = hostBuilder.Environment.ContentRootPath;
        _managedJsonSources = managedJsonSources.ToArray();
        _options = options;
        _logger = logger;
        _definitionScanner = new ConfigurationDefinitionScanner(
            new ConfigurationSchemaHasher(),
            options.SectionPathConvention);

        _seedConfiguration = BuildConfiguration(monicaValues: null);
        var runtimeContext = new ConfigurationRuntimeContext();
        runtimeContext.Capture(_seedConfiguration);
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
        (_seedConfiguration as IDisposable)?.Dispose();
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
        (_seedConfiguration as IDisposable)?.Dispose();
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
            _logger.LogInformation(
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

        var monicaValues = ProjectDocuments(definitions, documents);
        var effectiveConfiguration = BuildConfiguration(monicaValues);
        try
        {
            var loaded = new Dictionary<Type, object>();
            for (var i = 0; i < definitions.Length; i++)
            {
                loaded[missingTypes[i]] = BindOptions(missingTypes[i], definitions[i], effectiveConfiguration);
            }

            lock (_cacheLock)
            {
                foreach (var (optionsType, options) in loaded)
                {
                    _loadedOptions.TryAdd(optionsType, options);
                }
            }
        }
        finally
        {
            (effectiveConfiguration as IDisposable)?.Dispose();
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

    private Dictionary<string, string?> ProjectDocuments(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IReadOnlyList<ConfigurationEffectiveValueDocument> documents)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            var document = documents[i];
            if (!string.Equals(definition.DefinitionKey, document.DefinitionKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The Monica effective-value store returned document '{document.DefinitionKey}' for requested definition '{definition.DefinitionKey}'.");
            }

            try
            {
                foreach (var (key, value) in _documentEditor.Project(definition, document.Json))
                {
                    values[key] = value;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to project Monica effective JSON document for definition '{definition.DefinitionKey}'.",
                    ex);
            }
        }

        return values;
    }

    private IConfigurationRoot BuildConfiguration(IReadOnlyDictionary<string, string?>? monicaValues)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(_contentRootPath))
        {
            builder.SetBasePath(_contentRootPath);
        }

        builder.AddConfiguration(_bootstrapConfiguration);
        if (monicaValues is not null)
        {
            builder.AddInMemoryCollection(monicaValues);
        }

        foreach (var source in _managedJsonSources)
        {
            builder.AddJsonFile(source.Path, source.Optional, source.ReloadOnChange);
        }

        return builder.Build();
    }

    private object BindOptions(
        Type optionsType,
        ConfigurationDefinition definition,
        IConfiguration configuration)
    {
        try
        {
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
