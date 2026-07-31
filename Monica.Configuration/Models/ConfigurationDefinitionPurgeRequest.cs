namespace Monica.Configuration.Models;

/// <summary>
/// Requests permanent removal of one retired definition's mutable/current records.
/// </summary>
public sealed record ConfigurationDefinitionPurgeRequest
{
    /// <summary>
    /// Gets the stable definition key to purge.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the definition revision observed by the operator during purge preview.
    /// </summary>
    /// <remarks>
    /// The purge is rejected when the canonical definition has changed since preview.
    /// </remarks>
    public int ExpectedDefinitionRevision { get; init; }
}
