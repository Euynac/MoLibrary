namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Describes one callable tool or script surfaced by an agent capability.
/// </summary>
public sealed record AgentCapabilityToolInfo
{
    /// <summary>
    /// Creates tool metadata for management and reference-completion UI.
    /// </summary>
    public AgentCapabilityToolInfo(
        string name,
        string? description,
        bool isEnabled,
        string? parametersSchemaJson,
        IReadOnlyList<AgentCapabilityToolParameterInfo> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);

        Name = name.Trim();
        Description = description;
        IsEnabled = isEnabled;
        ParametersSchemaJson = parametersSchemaJson;
        Parameters = parameters;
    }

    /// <summary>
    /// Tool or script name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional tool description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Whether this tool is available to Monica agents.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Raw JSON schema for parameters, formatted for inspection.
    /// </summary>
    public string? ParametersSchemaJson { get; }

    /// <summary>
    /// Parsed top-level parameter rows from <see cref="ParametersSchemaJson"/>.
    /// </summary>
    public IReadOnlyList<AgentCapabilityToolParameterInfo> Parameters { get; }
}
