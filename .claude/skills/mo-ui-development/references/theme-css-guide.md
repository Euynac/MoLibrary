# MoLibrary Theme CSS Style Guide

## Overview

MoLibrary UI modules use a separated theme architecture, completely separating color management from style management to ensure theme extensibility and consistency.

## Architecture Principles

### 1. Dual-Layer Separation Architecture

**C# Theme Layer (ThemeProvider)**
- Responsible for color definitions and MudBlazor theme configuration
- Implements `IThemeProvider` interface
- Manages `PaletteLight` and `PaletteDark` color schemes
- Sets fonts, spacing, and other layout properties

**CSS Style Layer (Theme CSS)**
- Responsible for component styles, animations, border-radius, shadows, and other visual effects
- Uses MudBlazor CSS variables for colors; avoid custom color values unless necessary
- Unless necessary, no need to override color styles in rewritten MudBlazor components (already defined in C# theme colors)
- Defines component interaction effects and layouts

### 2. Color Management Standards

#### C# Layer Color Definition

```csharp
public class ThemeExample : IThemeProvider
{
    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#667eea",
                Secondary = "#f093fb",
                // ...other color definitions
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",
                Secondary = "#ff006e",
                // ...other color definitions
            }
        };
    }
}
```

> Note the contrast issue between --mud-palette-primary and --mud-palette-primary-text in dark mode.

#### CSS Layer Color Reference (For reference only, may not need to override colors again)

```css
.mud-button {
    background-color: var(--mud-palette-primary);
    color: var(--mud-palette-primary-text);
    border-color: var(--mud-palette-lines-inputs);
}
```

**Hardcoding color values in CSS is forbidden; must use MudBlazor variables.**

## File Structure Standards

```
MoLibrary.UI/
├── Themes/                          # C# theme definitions
│   ├── IThemeProvider.cs            # Theme provider interface
│   ├── ThemeMudBlazorDefault.cs     # MudBlazor original theme
│   ├── ThemeMoLibraryDefault.cs     # MoLibrary default theme
│   ├── ThemeGlassmorphic.cs         # Glassmorphic theme
│   └── ThemeRegistry.cs             # Theme registry management
└── wwwroot/css/
    ├── mo-theme-main.css            # Main entry file
    ├── themes/                      # Theme style files
    │   ├── mo-theme-default.css     # Default theme styles
    │   ├── mo-theme-glassmorphic.css # Glassmorphic theme styles
    │   └── mo-theme-mudblazor.css   # MudBlazor original styles (empty file)
    ├── components/                  # Component-specific styles (future expansion)
    └── tokens/                      # Design tokens (future expansion)
```

## CSS Variable Naming Standards

### 1. Component Style Variables

```css
:root {
    /* Button styles */
    --mo-button-border-radius: 12px;
    --mo-button-padding-x: 16px;
    --mo-button-padding-y: 8px;
    --mo-button-font-weight: 500;
    --mo-button-box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    --mo-button-transition: all 0.3s ease;

    /* Card styles */
    --mo-card-border-radius: 16px;
    --mo-card-padding: 24px;
    --mo-card-box-shadow: 0 4px 6px rgba(0,0,0,0.1);

    /* Input styles */
    --mo-input-border-radius: 8px;
    --mo-input-padding: 12px 16px;
    --mo-input-border-width: 2px;
}
```

### 2. Animation and Transition Variables

```css
:root {
    --mo-transition-fast: 150ms;
    --mo-transition-normal: 300ms;
    --mo-transition-slow: 450ms;
    --mo-animation-smooth: 400ms;
    --mo-animation-bounce: 600ms;
}
```

### 3. Spacing Variables

```css
:root {
    --mo-spacing-xs: 4px;
    --mo-spacing-sm: 8px;
    --mo-spacing-md: 16px;
    --mo-spacing-lg: 24px;
    --mo-spacing-xl: 32px;
}
```

## Theme Style Implementation Standards

### 1. Theme Class Selector Structure

```css
/* Theme-specific variables */
:root[data-theme="theme-name-light"],
.mo-theme-name-light {
    --mo-custom-variable: value;
}

:root[data-theme="theme-name-dark"],
.mo-theme-name-dark {
    --mo-custom-variable: value;
}

/* Common styles (applies to both modes) */
.mo-theme-name-light,
.mo-theme-name-dark {
    .mud-component {
        /* Style definitions */
    }
}

/* Light mode specific styles */
.mo-theme-name-light {
    .mud-component {
        /* Light mode specific styles */
    }
}

/* Dark mode specific styles */
.mo-theme-name-dark {
    .mud-component {
        /* Dark mode specific styles */
    }
}
```

### 2. Component Style Override Standards

**Button Component**

```css
.mud-button {
    border-radius: var(--mo-button-border-radius) !important;
    padding: var(--mo-button-padding-y) var(--mo-button-padding-x) !important;
    font-weight: var(--mo-button-font-weight) !important;
    transition: var(--mo-button-transition) !important;
}

.mud-button:hover {
    transform: translateY(-1px);
}
```

**Card Component**

```css
.mud-card {
    border-radius: var(--mo-card-border-radius) !important;
    box-shadow: var(--mo-card-box-shadow) !important;
    background-color: var(--mud-palette-surface);
    border: 1px solid var(--mud-palette-lines-default);
}
```

**Input Component**

```css
.mud-input-outlined .mud-input-outlined-border {
    border-color: var(--mud-palette-lines-inputs);
    border-radius: var(--mo-input-border-radius) !important;
}

.mud-input-outlined:hover .mud-input-outlined-border {
    border-color: var(--mud-palette-text-primary);
}

.mud-input-outlined.mud-input-focused .mud-input-outlined-border {
    border-color: var(--mud-palette-primary);
}
```

## Special Effects Implementation Standards

### 1. Glassmorphic Effect

```css
:root {
    --mo-glass-blur: 12px;
    --mo-glass-blur-heavy: 20px;
    --mo-glass-opacity: 0.85;
    --mo-glass-border-width: 1px;
}

.mo-glass-card {
    background: var(--mo-background-glass) !important;
    backdrop-filter: blur(var(--mo-glass-blur));
    -webkit-backdrop-filter: blur(var(--mo-glass-blur));
    border: var(--mo-glass-border-width) solid var(--mo-glass-border) !important;
    box-shadow: var(--mo-glass-shadow) !important;
}
```

### 2. Gradient Background Implementation

```css
.mo-theme-glassmorphic-light::before {
    content: '';
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    z-index: -10;
}
```

### 3. Neon Glow Effect (Dark Mode)

```css
.mo-theme-dark {
    --mo-neon-text-shadow: 0 0 10px currentColor, 0 0 20px currentColor;
    --mo-neon-box-shadow: 0 0 20px var(--mud-palette-primary);

    .mud-button-filled-primary:hover {
        animation: mo-neon-pulse 2s ease-in-out infinite;
    }
}

@keyframes mo-neon-pulse {
    0%, 100% {
        box-shadow: 0 0 20px var(--mud-palette-primary);
    }
    50% {
        box-shadow: 0 0 40px var(--mud-palette-primary);
    }
}
```

## Responsive Design Standards

### 1. Mobile Adaptation

```css
@media (max-width: 960px) {
    .mo-theme-glassmorphic-light,
    .mo-theme-glassmorphic-dark {
        --mo-glass-blur: 8px;
        --mo-glass-blur-heavy: 15px;
        --mo-card-padding: 16px;
        --mo-dialog-padding: 24px;
    }
}
```

### 2. Accessibility Support

```css
@media (prefers-reduced-motion: reduce) {
    .mo-theme-* * {
        animation: none !important;
        transition: none !important;
    }
}
```

## Performance Optimization Standards

### 1. Transition Animation Optimization

```css
/* Disable transitions on performance-sensitive components */
.mud-table *,
.mud-data-grid *,
.mud-treeview *,
.no-transition,
.no-transition * {
    transition: none !important;
}
```

### 2. Hardware Acceleration

```css
.mud-card:hover {
    transform: translateY(-1px) translateZ(0); /* Force hardware acceleration */
    will-change: transform; /* Hint browser to optimize */
}
```

## Utility Classes Standards

### 1. Glassmorphic Utility Classes

```css
.mo-glass-heavy {
    backdrop-filter: blur(var(--mo-glass-blur-heavy)) !important;
    -webkit-backdrop-filter: blur(var(--mo-glass-blur-heavy)) !important;
}

.mo-glass-light {
    backdrop-filter: blur(5px) !important;
    -webkit-backdrop-filter: blur(5px) !important;
}
```

### 2. Gradient Utility Classes

```css
.mo-gradient-bg {
    background: var(--mo-button-gradient) !important;
}

.mo-glow {
    box-shadow: var(--mo-hover-glow) !important;
}
```

## New Theme Creation Flow

### 1. C# Theme Class

```csharp
public class ThemeCustom : IThemeProvider
{
    public string Name => "custom";
    public string DisplayName => "Custom Theme";
    public string Description => "Theme description";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight() { /* Color definitions */ },
            PaletteDark = new PaletteDark() { /* Color definitions */ },
            LayoutProperties = new LayoutProperties() { /* Layout properties */ }
        };
    }
}
```

### 2. CSS Style File

```css
/* mo-theme-custom.css */
:root[data-theme="custom-light"],
.mo-theme-custom-light {
    /* Light mode variables */
}

:root[data-theme="custom-dark"],
.mo-theme-custom-dark {
    /* Dark mode variables */
}

.mo-theme-custom-light,
.mo-theme-custom-dark {
    /* Common component styles */
}
```

### 3. Theme Registration

```csharp
// Register in ThemeRegistry.cs
private static void RegisterDefaultThemes()
{
    RegisterTheme(new ThemeCustom());
}
```

### 4. CSS Import

```css
/* Import in mo-theme-main.css */
@import url('./themes/mo-theme-custom.css');
```

## Best Practices

### 1. Color Consistency

- Use `var(--mud-palette-primary)`
- Do not use `#667eea`

### 2. Style Override

- Use `!important` to ensure style priority
- Use CSS variables for maintainability
- Do not use inline styles

### 3. Performance Considerations

- Use transition animations reasonably
- Avoid complex animations on many elements
- Use hardware acceleration

### 4. Compatibility

- Provide `-webkit-` prefix support
- Support `prefers-reduced-motion`
- Mobile adaptation

## Important Notes

1. **!important usage**: Only use when overriding MudBlazor default styles
2. **CSS isolation**: Prefer CSS isolation over `<style>` tags
3. **Theme switching**: Ensure all styles correctly respond to theme switching
4. **Browser compatibility**: Special effects (like glassmorphic) require browser prefixes
5. **Dark mode**: Every theme must support both Light and Dark modes

## MudBlazor 8.9.0 Compatibility

### Typography Class Name Changes

| MudBlazor 7.x | MudBlazor 8.9.0 |
|---------------|-----------------|
| `new Default()` | `new DefaultTypography()` |
| `new H1()` | `new H1Typography()` |
| `new H2()` | `new H2Typography()` |
| `new H3()` | `new H3Typography()` |
| `new H4()` | `new H4Typography()` |
| `new H5()` | `new H5Typography()` |
| `new H6()` | `new H6Typography()` |
| `new Button()` | `new ButtonTypography()` |
| `new Body1()` | `new Body1Typography()` |
| `new Body2()` | `new Body2Typography()` |
| `new Caption()` | `new CaptionTypography()` |
| `new Subtitle1()` | `new Subtitle1Typography()` |
| `new Subtitle2()` | `new Subtitle2Typography()` |
| `new Overline()` | `new OverlineTypography()` |

### Data Type Changes

**FontWeight**: Must use string format
- Wrong: `FontWeight = 400` (int)
- Correct: `FontWeight = "400"` (string)

**LineHeight**: Must use string format
- Wrong: `LineHeight = 1.43` (double)
- Correct: `LineHeight = "1.43"` (string)

### Shadow.Elevation Array Requirement

MudBlazor 8.9.0 requires Shadow.Elevation array to have **26 elements** (indices 0-25).

### Palette Property Changes

- Use `BackgroundGray` (not `BackgroundGrey`)
- Removed properties: `ActionHover`, `ActionSelected`, `ActionSelectedHover`

### Theme Migration Checklist

- [ ] Typography uses correct class names (`DefaultTypography`, `H1Typography`, etc.)
- [ ] All FontWeight values use string format (`"400"` not `400`)
- [ ] All LineHeight values use string format (`"1.43"` not `1.43`)
- [ ] Palette property names correct (`BackgroundGray` not `BackgroundGrey`)
- [ ] Removed non-existent Palette properties
- [ ] Shadow.Elevation array has 26 elements (indices 0-25)
- [ ] ZIndex and other LayoutProperties structure correct
- [ ] Shadows property uses `new Shadow()` initialization

---

*This standard ensures consistency, maintainability, and extensibility of the MoLibrary theme system. All new themes should be developed following this standard.*
