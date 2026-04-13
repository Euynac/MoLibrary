using Monica.DependencyInjection.Models;
using Monica.DependencyInjection.Services.Support;

namespace Monica.DependencyInjection.Services;

/// <summary>
/// Provides finalized dependency-injection diagnostics snapshots to facades.
/// </summary>
internal sealed class DependencyInjectionDiagnosticsService(DependencyInjectionDiagnosticsRegistry registry)
{
    /// <summary>
    /// Gets the finalized dependency-injection diagnostics snapshot.
    /// </summary>
    public DependencyInjectionDiagnosticsSnapshot GetSnapshot()
    {
        return registry.GetSnapshot();
    }
}
