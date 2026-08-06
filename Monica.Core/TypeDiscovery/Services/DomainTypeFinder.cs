using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services.Support;

namespace Monica.Core.TypeDiscovery.Services;

/// <summary>
/// Type finder that discovers and filters application types.
/// </summary>
/// <remarks>
/// Initializes a new domain type finder.
/// </remarks>
/// <param name="options">The type finder options.</param>
/// <param name="logger">The host-owned logger used for discovery diagnostics.</param>
public class DomainTypeFinder(TypeFinderOptions options, ILogger<DomainTypeFinder> logger) : ITypeFinder
{
    private readonly object _discoveryGate = new();
    private volatile bool _assemblyListLoaded;
    private readonly Dictionary<string, TypeFinderAssemblyLoadFailure> _assemblyLoadFailures =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<Assembly> _assemblySnapshot = Array.Empty<Assembly>();
    private ExceptionDispatchInfo? _typeLoadException;
    private IReadOnlyList<Type>? _typeSnapshot;

    /// <summary>
    /// Loads the assembly list once.
    /// </summary>
    protected virtual void LoadAssemblies()
    {
        if (_assemblyListLoaded)
        {
            return;
        }

        lock (_discoveryGate)
        {
            if (_assemblyListLoaded)
            {
                return;
            }

            var scan = new TypeFinderAssemblyResolver(options, logger).Resolve();
            MergeAssemblyLoadFailures(scan.FailedLoads.Values);
            _assemblySnapshot = Array.AsReadOnly(scan.Assemblies.ToArray());

            logger.LogInformation(
                "Module system will scan the following assemblies:{Assemblies}",
                Environment.NewLine + string.Join(
                    Environment.NewLine,
                    _assemblySnapshot
                        .Select(static assembly => assembly.GetName().Name)
                        .OrderBy(static name => name)));

            _assemblyListLoaded = true;
        }
    }

    /// <summary>
    /// Gets all related assemblies.
    /// </summary>
    /// <returns>The related assemblies.</returns>
    public virtual IEnumerable<Assembly> GetAssemblies()
    {
        LoadAssemblies();
        return _assemblySnapshot;
    }

    /// <inheritdoc />
    public TypeFinderAssemblyAnalysis GetAssemblyAnalysis()
    {
        LoadAssemblies();
        lock (_discoveryGate)
        {
            return new TypeFinderAssemblyAnalysisBuilder(options, _assemblySnapshot, _assemblyLoadFailures).Build();
        }
    }

    public TypeFinderOptions Options => options;

    /// <summary>
    /// Gets the host's cached type snapshot from the related assemblies.
    /// </summary>
    /// <returns>
    /// A stable snapshot that can be enumerated repeatedly without invoking <see cref="Assembly.GetTypes"/> again.
    /// </returns>
    /// <remarks>
    /// Both successful partial loads and terminal scan exceptions are cached. This guarantees that an assembly is
    /// never repeatedly reflected because multiple module-system consumers request the host's business types.
    /// </remarks>
    public virtual IEnumerable<Type> GetTypes()
    {
        LoadAssemblies();

        if (Volatile.Read(ref _typeSnapshot) is { } snapshot)
        {
            return snapshot;
        }

        lock (_discoveryGate)
        {
            if (_typeSnapshot is not null)
            {
                return _typeSnapshot;
            }

            _typeLoadException?.Throw();

            try
            {
                _typeSnapshot = LoadTypeSnapshot();
                return _typeSnapshot;
            }
            catch (Exception exception)
            {
                _typeLoadException = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
        }
    }

    private void MergeAssemblyLoadFailures(IEnumerable<TypeFinderAssemblyLoadFailure> failures)
    {
        foreach (var failure in failures)
        {
            _assemblyLoadFailures[failure.Name] = failure;
        }
    }

    private IReadOnlyList<Type> LoadTypeSnapshot()
    {
        var discoveredTypes = new List<Type>();
        foreach (var assembly in _assemblySnapshot)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // Preserve successfully loaded types while recording the incomplete assembly for host diagnostics.
                types = exception.Types.OfType<Type>().ToArray();

                var loadFailure = TypeFinderAssemblyLoadFailure.CreateTypeScanFailure(assembly, exception);
                if (loadFailure is not null)
                {
                    _assemblyLoadFailures[loadFailure.Name] = loadFailure;

                    logger.LogWarning(
                        "Some types from assembly {AssemblyName} could not be loaded: {Exceptions}",
                        loadFailure.Name,
                        loadFailure.ErrorMessage);
                }
            }

            discoveredTypes.AddRange(types);
        }

        return Array.AsReadOnly(discoveredTypes.ToArray());
    }
}
