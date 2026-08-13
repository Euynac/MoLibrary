using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Declares finalized service contracts consumed by one module before host services are registered.
/// </summary>
/// <typeparam name="TOptions">The current module's finalized option type.</typeparam>
/// <remarks>
/// This descriptor can read finalized options and declare required service identities, but it cannot mutate the
/// service collection or any host lifecycle. Monica validates every requirement after all module-owned service
/// contributions have completed and before <c>AddMonica</c> returns.
/// </remarks>
public sealed class ModuleContractDescriptor<TOptions>
    where TOptions : class, IModuleOptions, new()
{
    private readonly ModuleRegistrationState _registration;

    internal ModuleContractDescriptor(ModuleRegistrationState registration)
    {
        _registration = registration;
        Options = (TOptions)registration.ModuleOption;
        Modules = new ModuleOptionReader(registration.Application, registration.ModuleType);
    }

    /// <summary>
    /// Gets the current module's finalized option object.
    /// </summary>
    public TOptions Options { get; }

    /// <summary>
    /// Gets the relationship-checked option reader for the current module.
    /// </summary>
    public IModuleOptionReader Modules { get; }

    /// <summary>
    /// Requires an unkeyed service registration.
    /// </summary>
    /// <typeparam name="TService">The exact service contract that must be registered.</typeparam>
    public void RequireService<TService>() where TService : class
    {
        _registration.RequireService(typeof(TService), isKeyed: false, serviceKey: null);
    }

    /// <summary>
    /// Requires a keyed service registration.
    /// </summary>
    /// <typeparam name="TService">The exact service contract that must be registered.</typeparam>
    /// <param name="serviceKey">The non-null service key selected by the finalized module options.</param>
    public void RequireKeyedService<TService>(object serviceKey) where TService : class
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        _registration.RequireService(typeof(TService), isKeyed: true, serviceKey);
    }
}
