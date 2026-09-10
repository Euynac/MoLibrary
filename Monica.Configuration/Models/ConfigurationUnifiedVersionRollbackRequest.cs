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
    /// Gets the fingerprint of the exact rollback preview reviewed by the caller.
    /// </summary>
    /// <remarks>
    /// This is an optimistic-concurrency value, not an authentication credential. The apply operation rejects the
    /// request when current values, schemas, destinations, or concurrency revisions no longer match the preview.
    /// </remarks>
    public required string PreviewFingerprint { get; init; }

    /// <summary>
    /// Gets the optional operator-facing label recorded with the rollback mutation group.
    /// </summary>
    /// <remarks>
    /// Leading and trailing whitespace is ignored. The service uses its default English label when this value is
    /// null, empty, or consists only of whitespace.
    /// </remarks>
    public string? Label { get; init; }

    /// <summary>
    /// Gets the optional operator reason recorded with the rollback mutation group.
    /// </summary>
    public string? Reason { get; init; }
}
