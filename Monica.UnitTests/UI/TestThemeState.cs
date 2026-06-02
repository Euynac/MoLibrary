using Monica.UI.Shell.State;
using Monica.UI.Theming;
using MudBlazor;

namespace Monica.UnitTests.UI;

/// <summary>
/// Provides a deterministic theme implementation for UI tests.
/// </summary>
public sealed class TestThemeState : IThemeState
{
    private bool _isDarkMode;
    private MonicaThemeKind _currentThemeKind = MonicaThemeKind.Default;

    /// <inheritdoc />
    public event Action? OnThemeChanged;

    /// <inheritdoc />
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode == value)
            {
                return;
            }

            _isDarkMode = value;
            OnThemeChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public MudTheme CurrentTheme { get; } = new();

    /// <inheritdoc />
    public MonicaThemeKind CurrentThemeKind
    {
        get => _currentThemeKind;
        set => SetTheme(value, _isDarkMode);
    }

    /// <inheritdoc />
    public void SetTheme(MonicaThemeKind themeKind, bool isDarkMode)
    {
        if (_currentThemeKind == themeKind && _isDarkMode == isDarkMode)
        {
            return;
        }

        _currentThemeKind = themeKind;
        _isDarkMode = isDarkMode;
        OnThemeChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    /// <inheritdoc />
    public string GetThemeCssClass() => "mo-theme-test";

    /// <inheritdoc />
    public string GetThemeDataAttribute() => IsDarkMode ? "test-dark" : "test-light";

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
