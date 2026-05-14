using MudBlazor;
using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Models;

/// <summary>
/// Tree node used by the configuration definition detail page.
/// </summary>
public sealed class ConfigurationNodeTreeItem : TreeItemData<ConfigurationNodeDefinition>
{
    /// <summary>
    /// Gets the logical path label for this node.
    /// </summary>
    public string LogicalPathLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the node summary label.
    /// </summary>
    public string SummaryLabel { get; init; } = string.Empty;
}
