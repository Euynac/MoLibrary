using MudBlazor;

namespace Monica.UI.Theming;

/// <summary>
/// Theme base class - provides the default code block theme implementation
/// </summary>
public abstract class ThemeDefinitionBase : IThemeDefinition
{
    /// <summary>
    /// Theme identity.
    /// </summary>
    public abstract MonicaThemeKind Kind { get; }
    
    /// <summary>
    /// Theme display name.
    /// </summary>
    public abstract string DisplayName { get; }
    
    /// <summary>
    /// Theme description.
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// Create MudTheme instance
    /// </summary>
    /// <returns>Configured MudTheme instance</returns>
    public abstract MudTheme CreateTheme();
    
    /// <summary>
    /// Get the Code Blocks theme in light mode
    /// Github theme is used by default, subclasses can be rewritten
    /// </summary>
    public virtual CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;
    
    /// <summary>
    /// Get the Code Blocks theme in dark mode
    /// The GithubDark theme is used by default and can be overridden by subclasses
    /// </summary>
    public virtual CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.GithubDark;
}
