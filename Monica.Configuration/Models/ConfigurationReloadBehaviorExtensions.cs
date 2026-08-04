namespace Monica.Configuration.Models;

/// <summary>
/// Provides shared rules for resolving and interpreting Monica configuration reload behavior metadata.
/// </summary>
public static class ConfigurationReloadBehaviorExtensions
{
    /// <summary>
    /// Resolves the operational reload behavior for a schema node.
    /// </summary>
    /// <param name="node">The schema node being evaluated.</param>
    /// <param name="definition">The owning configuration definition.</param>
    /// <returns>
    /// The nearest explicit override on the node or one of its ancestors; otherwise the definition behavior.
    /// </returns>
    public static ConfigurationReloadBehavior ResolveEffectiveReloadBehavior(
        this ConfigurationNodeDefinition node,
        ConfigurationDefinition definition)
    {
        var behavior = ConfigurationSchemaNavigator.ResolvePath(definition.Root, node.RelativePath)
                           ?.ReloadBehaviorOverride
                       ?? (node.ReloadBehavior is { } nodeBehavior and not ConfigurationReloadBehavior.Inherit
                           ? nodeBehavior
                           : null);

        return behavior
            ?? (definition.ReloadBehavior == ConfigurationReloadBehavior.Inherit
                ? ConfigurationReloadBehavior.Unknown
                : definition.ReloadBehavior);
    }

    /// <summary>
    /// Determines whether a configuration mutation should tell operators that a process restart is required.
    /// </summary>
    /// <param name="behavior">The effective reload behavior.</param>
    /// <returns>
    /// <see langword="true"/> when the value is known to be startup-bound or cannot be proven hot-reloadable.
    /// </returns>
    public static bool RequiresProcessRestart(this ConfigurationReloadBehavior behavior)
    {
        return behavior is ConfigurationReloadBehavior.Inherit
            or ConfigurationReloadBehavior.Unknown
            or ConfigurationReloadBehavior.RequiresRestart
            or ConfigurationReloadBehavior.StaticAfterStartup;
    }
}
