namespace Monica.Configuration.Models;

/// <summary>
/// Describes a changed configuration parameter for which no current publisher reports consumption.
/// </summary>
public sealed record ConfigurationParameterWithoutKnownConsumer
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
}
