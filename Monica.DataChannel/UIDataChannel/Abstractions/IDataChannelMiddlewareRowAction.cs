using Monica.DataChannel.UIDataChannel.Models;
using MudBlazor;

namespace Monica.DataChannel.UIDataChannel.Abstractions;

/// <summary>
/// Contributes a host-owned action to a middleware row in the DataChannel status UI.
/// </summary>
public interface IDataChannelMiddlewareRowAction
{
    /// <summary>
    /// Gets the MudBlazor icon rendered for the action.
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// Gets the semantic color rendered for the action.
    /// </summary>
    Color Color { get; }

    /// <summary>
    /// Gets the localized tooltip and accessible label for the action.
    /// </summary>
    string Tooltip { get; }

    /// <summary>
    /// Gets the display order among contributed actions.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Determines whether the action applies to the supplied middleware row.
    /// </summary>
    bool IsVisible(string channelId, PipelineComponentInfo middleware);

    /// <summary>
    /// Executes the action for the supplied middleware row.
    /// </summary>
    Task ExecuteAsync(string channelId, PipelineComponentInfo middleware);
}
