namespace Monica.Configuration.Models;

/// <summary>
/// Requests applying a previously reviewed unified-version rollback plan.
/// </summary>
public sealed record ConfigurationUnifiedVersionRollbackRequest
{
    /// <summary>
    /// Gets the unified configuration version to restore.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the preview fingerprint reviewed by the caller.
    /// </summary>
    public required string PlanToken { get; init; }

    /// <summary>
    /// Gets the optional operator reason recorded with the rollback mutation group.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets whether the operator explicitly acknowledged changed values whose historical schema hash differs but
    /// whose JSON remains valid under the current schema.
    /// </summary>
    /// <remarks>
    /// This flag never bypasses current-schema validation and cannot force incompatible values into a store.
    /// </remarks>
    public bool AcknowledgeCompatibleSchemaDrift { get; init; }
}
