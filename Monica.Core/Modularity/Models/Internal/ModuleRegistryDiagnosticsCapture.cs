using System.Collections.Immutable;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.State;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Carries one detached registry observation whose mutable host-owned objects never escape the registry lock.
/// </summary>
internal sealed record ModuleRegistryDiagnosticsCapture(
    long Revision,
    ModuleCompositionDiagnosticsState Composition,
    ImmutableArray<ModuleRegistrationDiagnosticsState> Registrations,
    ImmutableArray<ModuleRuntimeDiagnosticsState> RuntimeModules,
    ImmutableArray<ModuleRegistrationErrorDiagnosticsState> Errors);

/// <summary>Contains scalar diagnostics state detached from one module registration.</summary>
internal sealed record ModuleRegistrationDiagnosticsState(
    Type ModuleType,
    ModulePhase ModulePhase,
    int Order,
    bool IsWebModule,
    bool RequiresWebHost,
    string? WebHostRequirementReason,
    string? DisabledReason);

/// <summary>Contains detached runtime availability and order for one successfully registered module.</summary>
internal sealed record ModuleRuntimeDiagnosticsState(Type ModuleType, int Order);

/// <summary>Contains sanitized scalar evidence detached from one registration failure.</summary>
internal sealed record ModuleRegistrationErrorDiagnosticsState(
    Type ModuleType,
    ModuleRegistrationErrorType ErrorType,
    ModulePhase? Phase,
    string? WorkItemId);
