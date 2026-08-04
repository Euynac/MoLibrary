namespace Monica.Configuration.Models;

/// <summary>
/// Describes the effective reload impact of a changed parameter on one logical service.
/// </summary>
/// <remarks>
/// Publisher metadata establishes consumption of the owning definition; it does not prove that the service reads this
/// exact parameter at runtime.
/// </remarks>
public sealed record ConfigurationAffectedParameter
{
    /// <summary>
    /// Gets the stable configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the operator-facing configuration definition name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the exact logical path of the changed parameter.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the operator-facing parameter name resolved from the current schema.
    /// </summary>
    public required string ParameterDisplayName { get; init; }

    /// <summary>
    /// Gets how Monica obtained the reload-behavior evidence for this service and parameter.
    /// </summary>
    public ConfigurationReloadBehaviorObservationKind ObservationKind { get; init; }

    /// <summary>
    /// Gets the effective reload behavior for this service and parameter.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }

    /// <summary>
    /// Gets whether operators should restart the consuming service after saving the parameter.
    /// </summary>
    /// <remarks>
    /// Unknown behavior is treated conservatively and therefore also requires a restart advisory.
    /// </remarks>
    public bool RequiresRestart => ReloadBehavior.RequiresProcessRestart();
}
