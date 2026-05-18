using System.Reflection;
using Monica.Core;

namespace Monica.UnitTests.Modularity;

/// <summary>
/// Isolates Monica module-system state between tests.
/// </summary>
public sealed class ModuleTestScope : IDisposable
{
    private readonly MonicaApplication _application;
    private bool _disposed;

    private ModuleTestScope(MonicaApplication application)
    {
        _application = application;
    }

    /// <summary>
    /// Creates an isolated module test scope and narrows type discovery to the supplied assemblies.
    /// </summary>
    public static ModuleTestScope Create(params Assembly[] assemblies)
    {
        var application = MonicaApplication.CreateScoped();
        Reset();

        Mo.ConfigTypeDiscovery(options =>
        {
            options.ExcludeDefault();
            if (assemblies.Length > 0)
            {
                options.Add(assemblies);
            }
        });

        return new ModuleTestScope(application);
    }

    /// <summary>
    /// Clears Monica module-system state for the current Monica application.
    /// </summary>
    public static void Reset()
    {
        MonicaApplication.Current.ResetModuleState();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Reset();
        Mo.ConfigTypeDiscovery();
        _application.Dispose();
        _disposed = true;
    }
}
