# Monica Theme CSS Guide

## Current Architecture

Monica's theme system is split between C# theme definitions and theme CSS.

### C# theme layer

- Theme definitions live under `Monica.UI/Theming/Definitions/`
- Each theme implements `IThemeDefinition`
- `ThemeCatalog` registers the available definitions
- `ThemeState` owns the active theme name, dark-mode state, and generated CSS/data-theme identifiers
- `ThemeProviderHost` renders the wrapper that applies:
  - `class="mo-theme-{name}-{mode}"`
  - `data-theme="{name}-{mode}"`

### CSS theme layer

- Theme CSS lives under `Monica.UI/wwwroot/css/themes/`
- `Monica.UI/wwwroot/css/mo-theme-main.css` imports the per-theme CSS files
- CSS should define theme tokens, surfaces, borders, typography accents, and shared component structure
- Color palettes, typography defaults, and MudBlazor tokens should still be rooted in the `MudTheme` returned by the C# definition

## Theme Authoring Workflow

1. Add a new `IThemeDefinition` under `Monica.UI/Theming/Definitions/`.
2. Register it in `ThemeCatalog`.
3. Add localized name and description entries for theme selection UI.
4. Create `Monica.UI/wwwroot/css/themes/mo-theme-{name}.css`.
5. Import the new CSS file from `Monica.UI/wwwroot/css/mo-theme-main.css`.
6. If the theme needs a custom markdown/code-block presentation, add the corresponding markdown CSS file and register the code-block theme through the definition.
7. Verify persisted theme switching through `ThemeState` and browser storage.

## CSS Structure Rules

### 1. Define tokens on both the wrapper class and the `data-theme` root

```css
:root[data-theme="example-light"],
.mo-theme-example-light {
    --mo-example-panel: rgba(255, 255, 255, 0.92);
}

:root[data-theme="example-dark"],
.mo-theme-example-dark {
    --mo-example-panel: rgba(12, 18, 22, 0.92);
}
```

This keeps the theme usable for:

- normal app runtime
- DOM inspection
- browser-level overrides
- tools that read only `data-theme`

### 2. Keep palette ownership in C#

Use the C# `MudTheme` definition for:

- palette colors
- typography defaults
- shadows
- layout tokens that MudBlazor consumes directly

Use CSS for:

- custom background treatments
- surface shells
- borders and radii
- component-specific visual language
- light/dark-only decorative behavior

### 3. Prefer shared MudBlazor selectors over page-specific hooks

Theme CSS should target stable shared structures such as:

- `.mud-card`
- `.mud-dialog`
- `.mud-data-grid`
- `.mud-tabs`
- `.mud-input-outlined`

Do not rely on page-only classes when the intent is a reusable theme behavior. If a temporary page-specific hook is added during diagnosis, remove it after the shared selector is in place.

### 4. Keep theme CSS component-oriented

A good theme file usually looks like:

1. theme tokens
2. shell/background
3. navigation/chrome
4. shared surfaces
5. forms
6. tables/data grids
7. dialogs, menus, snackbars, overlays

Avoid scattering one-off page fixes throughout the theme file.

## High-Risk Component Areas

### `MudTabs` with `ApplyEffectsToContainer="true"`

MudBlazor applies rounded, outlined, and elevation classes to the root `.mud-tabs` element in this mode.

If the theme needs a visible tab shell, inspect and style:

- the root `.mud-tabs`
- `.mud-tabs-tabbar`
- `.mud-tabs-panels`

Do not assume styling only the tabbar is enough.

### `MudDataGrid` header controls

MudBlazor intentionally hides several header affordances until hover. If the theme causes the header to look blank, inspect:

- `.sort-direction-icon`
- `.column-options-icon`
- `.drag-icon-options`
- `.column-options .mud-menu .mud-icon-button-label`

When needed, provide a visible default state and a stronger hover state at the theme level.

### Snackbars and overlays

If you replace the default surface/background treatment, also verify:

- message text contrast
- action button contrast
- icon contrast
- success/info/warning/error variants

Changing only the container background is often not enough.

## Verification Workflow

1. Persist the target theme through `IBrowserStorage` or manual `localStorage` setup.
2. Verify the actual runtime state by checking:
   - wrapper class
   - `data-theme`
   - computed styles
3. Inspect the live DOM before editing selectors.
4. If component behavior is unclear, inspect MudBlazor source before guessing.
5. Verify both light and dark modes.
6. Verify the same component type on more than one page when the component is reused across the app.

## Manual Theme Debugging Note

When using Playwright or browser DevTools, remember that Monica's `IBrowserStorage` prefixes keys with `mo:`.

Application code reads:

```csharp
await BrowserStorage.GetAsync<ThemeData?>("theme:data", null);
```

Manual browser storage debugging must therefore set:

```text
mo:theme:data
```

not just `theme:data`.

## Related References

- `references/theme-authoring-pitfalls.md`
- `references/browser-storage-guide.md`
