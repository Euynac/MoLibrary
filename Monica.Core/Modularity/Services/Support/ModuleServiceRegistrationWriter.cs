using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Diagnostics.Models;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Performs deterministic service mutations using an index over service type and key.
/// </summary>
public sealed class ModuleServiceRegistrationWriter
{
    private readonly IServiceCollection _services;
    private readonly Dictionary<ServiceIdentity, int> _lastIndexByIdentity;
    private int _addedCount;
    private int _replacedCount;
    private int _skippedCount;

    internal ModuleServiceRegistrationWriter(IServiceCollection services)
    {
        _services = services;
        _lastIndexByIdentity = new Dictionary<ServiceIdentity, int>(services.Count);
        for (var index = 0; index < services.Count; index++)
        {
            _lastIndexByIdentity[ServiceIdentity.From(services[index])] = index;
        }
    }

    /// <summary>
    /// Adds a descriptor only when the same service type and service key have not already been registered.
    /// </summary>
    public bool TryAdd(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var identity = ServiceIdentity.From(descriptor);
        if (_lastIndexByIdentity.ContainsKey(identity))
        {
            _skippedCount++;
            return false;
        }

        AddCore(descriptor);
        return true;
    }

    /// <summary>
    /// Determines whether a descriptor with the same service identity is already registered.
    /// </summary>
    /// <param name="serviceType">The exposed service type.</param>
    /// <param name="serviceKey">The optional keyed-service identity.</param>
    /// <param name="isKeyedService">Whether the lookup targets the keyed-service namespace.</param>
    public bool Contains(Type serviceType, object? serviceKey = null, bool isKeyedService = false)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _lastIndexByIdentity.ContainsKey(new ServiceIdentity(serviceType, isKeyedService, serviceKey));
    }

    /// <summary>
    /// Adds a descriptor and updates the service identity index.
    /// </summary>
    public void Add(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        AddCore(descriptor);
    }

    /// <summary>
    /// Replaces the last descriptor with the same service identity, or appends the descriptor when absent.
    /// </summary>
    public void Replace(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var identity = ServiceIdentity.From(descriptor);
        if (_lastIndexByIdentity.TryGetValue(identity, out var index))
        {
            _services[index] = descriptor;
            _replacedCount++;
            return;
        }

        AddCore(descriptor);
    }

    internal TypeDiscoveryServiceRegistrationStatistics GetStatistics()
    {
        return new TypeDiscoveryServiceRegistrationStatistics
        {
            AddedCount = _addedCount,
            ReplacedCount = _replacedCount,
            SkippedCount = _skippedCount
        };
    }

    private void AddCore(ServiceDescriptor descriptor)
    {
        _services.Add(descriptor);
        _lastIndexByIdentity[ServiceIdentity.From(descriptor)] = _services.Count - 1;
        _addedCount++;
    }

    private readonly record struct ServiceIdentity(Type ServiceType, bool IsKeyedService, object? ServiceKey)
    {
        internal static ServiceIdentity From(ServiceDescriptor descriptor)
        {
            return new ServiceIdentity(
                descriptor.ServiceType,
                descriptor.IsKeyedService,
                descriptor.IsKeyedService ? descriptor.ServiceKey : null);
        }
    }
}
