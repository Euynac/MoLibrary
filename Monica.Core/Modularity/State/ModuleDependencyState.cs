using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module dependency graph data for one Monica application instance.
/// </summary>
internal sealed class ModuleDependencyState
{
    private readonly Dictionary<Type, ModuleKey> _moduleKeysByType = [];
    private readonly Dictionary<ModuleKey, Type> _moduleTypesByKey = [];
    private readonly Dictionary<ModuleKey, HashSet<ModuleKey>> _dependenciesByModule = [];

    internal IEnumerable<ModuleKey> MappedModuleKeys => _moduleTypesByKey.Keys;

    internal IEnumerable<(ModuleKey Module, IReadOnlySet<ModuleKey> Dependencies)> DependencyEntries
    {
        get
        {
            foreach (var (module, dependencies) in _dependenciesByModule)
            {
                yield return (module, dependencies);
            }
        }
    }

    internal IReadOnlyDictionary<Type, ModuleKey> CreateModuleKeysByTypeSnapshot()
    {
        return _moduleKeysByType.ToFrozenDictionary();
    }

    internal IReadOnlyDictionary<ModuleKey, Type> CreateModuleTypesByKeySnapshot()
    {
        return _moduleTypesByKey.ToFrozenDictionary();
    }

    internal IReadOnlyDictionary<ModuleKey, IReadOnlySet<ModuleKey>> CreateDependenciesByModuleSnapshot()
    {
        return _dependenciesByModule.ToFrozenDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlySet<ModuleKey>)entry.Value.ToFrozenSet());
    }

    internal bool TryGetModuleKey(Type moduleType, out ModuleKey moduleKey)
    {
        return _moduleKeysByType.TryGetValue(moduleType, out moduleKey);
    }

    internal bool TryGetModuleType(ModuleKey moduleKey, [NotNullWhen(true)] out Type? moduleType)
    {
        return _moduleTypesByKey.TryGetValue(moduleKey, out moduleType);
    }

    internal bool TryGetDependencies(
        ModuleKey moduleKey,
        [NotNullWhen(true)] out IReadOnlySet<ModuleKey>? dependencies)
    {
        if (_dependenciesByModule.TryGetValue(moduleKey, out var storedDependencies))
        {
            dependencies = storedDependencies;
            return true;
        }

        dependencies = null;
        return false;
    }

    internal void RegisterMapping(Type moduleType, ModuleKey moduleKey)
    {
        if (_moduleKeysByType.TryGetValue(moduleType, out var existingKey) && existingKey != moduleKey)
        {
            throw new InvalidOperationException(
                $"Module type '{moduleType.FullName}' is already mapped to key '{existingKey}' and cannot be remapped to '{moduleKey}'.");
        }

        if (_moduleTypesByKey.TryGetValue(moduleKey, out var existingType) && existingType != moduleType)
        {
            throw new InvalidOperationException(
                $"Module key '{moduleKey}' is already mapped to type '{existingType.FullName}' and cannot also map to '{moduleType.FullName}'.");
        }

        _moduleKeysByType[moduleType] = moduleKey;
        _moduleTypesByKey[moduleKey] = moduleType;
    }

    internal void AddDependency(ModuleKey moduleKey, ModuleKey dependencyKey)
    {
        if (moduleKey == dependencyKey)
        {
            return;
        }

        if (!_dependenciesByModule.TryGetValue(moduleKey, out var dependencies))
        {
            dependencies = [];
            _dependenciesByModule[moduleKey] = dependencies;
        }

        dependencies.Add(dependencyKey);
    }

    /// <summary>
    /// Clears dependency graph data.
    /// </summary>
    internal void Clear()
    {
        _moduleKeysByType.Clear();
        _moduleTypesByKey.Clear();
        _dependenciesByModule.Clear();
    }
}
