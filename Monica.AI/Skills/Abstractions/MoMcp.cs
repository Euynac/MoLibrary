using Monica.Core.Modularity.Models;

namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Placeholder base for future MCP server integrations.
/// </summary>
public abstract class MoMcp
{
    /// <summary>
    /// MCP service name.
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Module keys that must be loaded for this MCP service to be available.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Determines whether this MCP service is available at startup.
    /// </summary>
    public virtual bool IsEnabled => true;
}
