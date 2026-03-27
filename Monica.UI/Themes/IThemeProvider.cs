using MudBlazor;

namespace Monica.UI.Themes;

/// <summary>
/// Theme provider interface
/// </summary>
public interface IThemeProvider
{
    /// <summary>
    /// Topic name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Topic display name
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Topic description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Create MudTheme instance
    /// </summary>
    /// <returns>Configured MudTheme instance</returns>
    MudTheme CreateTheme();
    
    /// <summary>
    /// Get the Code Blocks theme in light mode
    /// </summary>
    CodeBlockTheme LightCodeBlockTheme { get; }
    
    /// <summary>
    /// Get the Code Blocks theme in dark mode
    /// </summary>
    CodeBlockTheme DarkCodeBlockTheme { get; }
}