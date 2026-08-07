using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Binding;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Bootstrap;

internal sealed class MonicaEffectiveOptionsSnapshotLoader : IDisposable, IAsyncDisposable
{
    private readonly IConfigurationEffectiveValueStore _store;
    private readonly IConfiguration _hostConfiguration;
    private readonly string _contentRootPath;
    private readonly IReadOnlyList<ManagedJsonConfigurationSourceRegistration> _managedJsonSources;
    private readonly MonicaEffectiveOptionsSnapshotOptions _options;
    private readonly ConfigurationDefinitionScanner _definitionScanner;
    private readonly ConfigurationEffectiveValueSeedFactory _seedFactory;
    private readonly ConfigurationEffectiveValueDocumentEditor _documentEditor;
    private readonly ILogger _logger;
    private bool _disposed;

    public MonicaEffectiveOptionsSnapshotLoader(
        MonicaBootstrapConfiguration bootstrapConfiguration,
        ConfigurationSectionPathConvention sectionPathConvention,
        MonicaEffectiveOptionsSnapshotOptions options,
        IConfigurationEffectiveValueStore store,
        IReadOnlyList<ManagedJsonConfigurationSourceRegistration> managedJsonSources,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(managedJsonSources);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _hostConfiguration = bootstrapConfiguration.HostConfiguration;
        _contentRootPath = bootstrapConfiguration.ContentRootPath;
        _managedJsonSources = managedJsonSources.ToArray();
        _options = options;
        _logger = logger;
        _definitionScanner = new ConfigurationDefinitionScanner(
            new ConfigurationSchemaHasher(),
            sectionPathConvention);

        var runtimeContext = new ConfigurationRuntimeContext();
        runtimeContext.Capture(bootstrapConfiguration);
        _seedFactory = new ConfigurationEffectiveValueSeedFactory(runtimeContext);
        _documentEditor = new ConfigurationEffectiveValueDocumentEditor(
            new ConfigurationEffectiveValuePatchEngine(),
            new ConfigurationStoredValueCodec());
    }

    public MonicaEffectiveOptionsSnapshot Load(IReadOnlyCollection<Type> optionsTypes)
    {
        return LoadAsync(optionsTypes, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<MonicaEffectiveOptionsSnapshot> LoadAsync(
        IReadOnlyCollection<Type> optionsTypes,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(optionsTypes);

        var requestedTypes = NormalizeRequestedTypes(optionsTypes);
        var loadedOptions = await LoadOptionsAsync(requestedTypes, cancellationToken);
        return new MonicaEffectiveOptionsSnapshot(loadedOptions);
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
        else if (_store is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        }
        else if (_store is IDisposable disposable)
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

    private async Task<IReadOnlyDictionary<Type, object>> LoadOptionsAsync(
        IReadOnlyList<Type> optionsTypes,
        CancellationToken cancellationToken)
    {
        var definitions = optionsTypes
            .Select(ScanOptionsType)
            .ToArray();

        var seeds = definitions
            .Select(definition => new ConfigurationEffectiveValueSeed(
                definition,
                () => _seedFactory.CreateSeedJson(definition)))
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
            var loadedOptions = new Dictionary<Type, object>();
            for (var i = 0; i < definitions.Length; i++)
            {
                loadedOptions[optionsTypes[i]] = BindOptions(
                    optionsTypes[i],
                    definitions[i],
                    effectiveConfiguration);
            }

            return loadedOptions;
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

    private IConfigurationRoot BuildConfiguration(IReadOnlyDictionary<string, string?> monicaValues)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(_contentRootPath))
        {
            builder.SetBasePath(_contentRootPath);
        }

        builder.AddConfiguration(_hostConfiguration);
        builder.AddInMemoryCollection(monicaValues);

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

}
