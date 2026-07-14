using System.Reflection;
using Monica.Core.Modularity.Abstractions;

namespace Monica.UnitTests.Modularity;

/// <summary>
/// Stores deterministic business-type discovery inputs for a lightweight Monica test host.
/// </summary>
public sealed class ModuleTestScope : IDisposable
{
    private readonly Assembly[] _assemblies;

    private ModuleTestScope(Assembly[] assemblies)
    {
        _assemblies = assemblies;
    }

    /// <summary>
    /// Creates a module test scope that can configure one host-bound Monica builder.
    /// </summary>
    /// <param name="assemblies">Assemblies included in business-type discovery.</param>
    /// <returns>The test scope.</returns>
    public static ModuleTestScope Create(params Assembly[] assemblies)
    {
        return new ModuleTestScope(assemblies);
    }

    /// <summary>
    /// Applies this scope's deterministic type-discovery configuration to a Monica builder.
    /// </summary>
    /// <param name="builder">The host-bound Monica builder.</param>
    public void Configure(IMonicaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureTypeDiscovery(options =>
        {
            options.ExcludeDefault();
            if (_assemblies.Length > 0)
            {
                options.Add(_assemblies);
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
