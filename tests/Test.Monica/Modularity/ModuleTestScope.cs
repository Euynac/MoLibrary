using System.Diagnostics;
using System.Reflection;
using Monica.Core;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;

namespace Test.Monica.Modularity;

/// <summary>
/// Isolates Monica module-system static state between tests.
/// </summary>
public sealed class ModuleTestScope : IDisposable
{
    private bool _disposed;

    private ModuleTestScope()
    {
    }

    /// <summary>
    /// Creates an isolated module test scope and narrows type discovery to the supplied assemblies.
    /// </summary>
    public static ModuleTestScope Create(params Assembly[] assemblies)
    {
        Reset();

        Mo.Options.ConfigTypeFinder(options =>
        {
            options.ExcludeDefault();
            if (assemblies.Length > 0)
            {
                options.Add(assemblies);
            }
        });

        return new ModuleTestScope();
    }

    /// <summary>
    /// Clears Monica module-system static state.
    /// </summary>
    public static void Reset()
    {
        ModuleRegistry.ModuleRegisterErrors.Clear();
        ModuleRegistry.ModuleSnapshots.Clear();
        ModuleRegistry.ModuleRegisterContextDict.Clear();

        ModuleDependencyAnalyzer.ModuleTypeToKeyMap.Clear();
        ModuleDependencyAnalyzer.ModuleKeyToTypeDict.Clear();
        ModuleDependencyAnalyzer.ModuleDependencyMap.Clear();

        ResetInitializationProfiler();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Reset();
        Mo.Options.ConfigTypeFinder();
        _disposed = true;
    }

    private static void ResetInitializationProfiler()
    {
        var profilerType = typeof(ModuleInitializationProfiler);

        ((Stopwatch?)profilerType
            .GetField("SystemStopwatch", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null))
            ?.Reset();

        ClearPrivateCollectionField(profilerType, "PhaseStopwatches");
        ClearPrivateCollectionField(profilerType, "PhaseInitializationOrder");
        ClearPrivateCollectionField(profilerType, "ModuleProfiles");

        profilerType
            .GetField("_isStarted", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, false);
    }

    private static void ClearPrivateCollectionField(Type type, string fieldName)
    {
        if (type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) is System.Collections.IList list)
        {
            list.Clear();
            return;
        }

        if (type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) is System.Collections.IDictionary dictionary)
        {
            dictionary.Clear();
        }
    }
}
