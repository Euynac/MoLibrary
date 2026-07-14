using System.Collections.ObjectModel;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module registration lifecycle data for one Monica application instance.
/// </summary>
internal sealed class ModuleRegistryState
{
    private readonly List<ModuleRegistrationError> _registrationErrors = [];
    private readonly List<ModuleRuntimeSnapshot> _runtimeSnapshots = [];
    private readonly Dictionary<Type, ModuleRegistrationState> _registrations = [];

    internal ModuleRegistryState()
    {
        RegistrationErrors = _registrationErrors.AsReadOnly();
        RuntimeSnapshots = _runtimeSnapshots.AsReadOnly();
        Registrations = new ReadOnlyDictionary<Type, ModuleRegistrationState>(_registrations);
    }

    /// <summary>
    /// Gets module registration errors captured during the current registration lifecycle.
    /// </summary>
    internal IReadOnlyList<ModuleRegistrationError> RegistrationErrors { get; }

    /// <summary>
    /// Gets module snapshots captured after successful registration.
    /// </summary>
    internal IReadOnlyList<ModuleRuntimeSnapshot> RuntimeSnapshots { get; }

    /// <summary>
    /// Gets registration information for every module type registered in this application instance.
    /// </summary>
    internal IReadOnlyDictionary<Type, ModuleRegistrationState> Registrations { get; }

    /// <summary>
    /// Adds a registration error to this host's lifecycle state.
    /// </summary>
    internal void AddRegistrationError(ModuleRegistrationError error)
    {
        _registrationErrors.Add(error);
    }

    /// <summary>
    /// Clears registration errors before a registration lifecycle starts.
    /// </summary>
    internal void ClearRegistrationErrors()
    {
        _registrationErrors.Clear();
    }

    /// <summary>
    /// Adds a module registration if its type is not already known.
    /// </summary>
    internal bool TryAddRegistration(Type moduleType, ModuleRegistrationState registration)
    {
        return _registrations.TryAdd(moduleType, registration);
    }

    /// <summary>
    /// Attempts to retrieve a module registration by type.
    /// </summary>
    internal bool TryGetRegistration(
        Type moduleType,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ModuleRegistrationState? registration)
    {
        return _registrations.TryGetValue(moduleType, out registration);
    }

    /// <summary>
    /// Adds the finalized module runtime snapshots.
    /// </summary>
    internal void AddRuntimeSnapshots(IEnumerable<ModuleRuntimeSnapshot> snapshots)
    {
        _runtimeSnapshots.AddRange(snapshots);
    }

    /// <summary>
    /// Clears all module registration lifecycle data.
    /// </summary>
    internal void Clear()
    {
        _registrationErrors.Clear();
        _runtimeSnapshots.Clear();
        _registrations.Clear();
    }
}
