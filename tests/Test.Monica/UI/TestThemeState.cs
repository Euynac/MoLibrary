using Monica.UI.Shell.State;
using MudBlazor;

namespace Test.Monica.UI;

/// <summary>
/// Provides a deterministic theme implementation for UI tests.
/// </summary>
public sealed class TestThemeState : IThemeState
{
    /// <inheritdoc />
    public event Action? OnThemeChanged;

    /// <inheritdoc />
    public bool IsDarkMode { get; set; }

    /// <inheritdoc />
    public MudTheme CurrentTheme { get; } = new();

    /// <inheritdoc />
    public string CurrentThemeName { get; set; } = "test";

    /// <inheritdoc />
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
    }

    /// <inheritdoc />
    public string GetThemeCssClass() => "mo-theme-test";

    /// <inheritdoc />
    public string GetThemeDataAttribute() => "test-light";

    /// <inheritdoc />
    public string GetColorHex(Color color)
    {
        return color switch
        {
            Color.Primary => "#1976d2",
            Color.Secondary => "#9c27b0",
            Color.Info => "#0288d1",
            Color.Success => "#2e7d32",
            Color.Warning => "#ed6c02",
            Color.Error => "#d32f2f",
            Color.Surface => "#9e9e9e",
            Color.Default => "#6b7280",
            _ => "#6b7280"
        };
    }
}
