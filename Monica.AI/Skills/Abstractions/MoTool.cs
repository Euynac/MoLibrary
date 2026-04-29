using Microsoft.Extensions.AI;
using Monica.Core.Modularity.Models;

namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Base class for standalone AI tools that are not part of a larger skill.
/// </summary>
public abstract class MoTool : AITool
{
    /// <inheritdoc />
    public abstract override string Name { get; }

    /// <inheritdoc />
    public abstract override string Description { get; }

    /// <summary>
    /// Module keys that must be loaded for this tool to be available.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Determines whether this tool is available at startup.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// The tool body. Use an <see cref="IServiceProvider"/> parameter when scoped services are needed.
    /// </summary>
    public abstract Delegate InvokeAsync { get; }
}
