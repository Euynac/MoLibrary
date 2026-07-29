namespace Monica.Core.HostedService.Abstractions.Internal;

/// <summary>
/// Owns the reversible runtime registration created for a Monica hosted service.
/// </summary>
internal interface IHostedServiceRuntimeOwner
{
    /// <summary>
    /// Releases a runtime registration that has not been committed to the hosted-service registry.
    /// </summary>
    void ReleaseRuntimeInfo();
}
