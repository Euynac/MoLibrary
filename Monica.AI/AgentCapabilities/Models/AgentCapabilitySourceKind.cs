namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Identifies where a skill entry comes from.
/// </summary>
public enum AgentCapabilitySourceKind
{
    /// <summary>
    /// The skill is defined in code.
    /// </summary>
    CodeDefined,

    /// <summary>
    /// The skill is discovered from an external file-based skill package.
    /// </summary>
    ExternalFile
}
