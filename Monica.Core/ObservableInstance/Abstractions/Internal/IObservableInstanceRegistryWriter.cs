namespace Monica.Core.ObservableInstance.Abstractions.Internal;

/// <summary>
/// Owns reversible mutations of the host-scoped observable-instance registry.
/// </summary>
internal interface IObservableInstanceRegistryWriter
{
    /// <summary>
    /// Removes and disposes the tracker with the exact instance identity.
    /// </summary>
    /// <param name="instanceId">The exact registered instance identity.</param>
    /// <returns><see langword="true"/> when a tracker was removed; otherwise, <see langword="false"/>.</returns>
    bool Unregister(string instanceId);
}
