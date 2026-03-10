---
name: mo-ui-development
description: This skill should be used when the user asks to create or modify Blazor UI components, build MudBlazor pages, style MudBlazor components, fix CSS isolation, customize themes, migrate to MudBlazor v9, validate MudBlazor CSS variables, implement browser storage with IMoBrowserStorage, or implement localization/i18n patterns in Monica UI modules.
version: 2.2.0
---

# Monica UI Development Guide

This skill is for Monica Blazor UI work with MudBlazor v9.

All script paths in this document are relative to the `mo-ui-development` skill directory.

## Mandatory First Step (Required Every Time)

Run the source check script before any UI implementation:

```bash
python scripts/check_mudblazor_source.py
```

If it fails, stop implementation and ask the user to download MudBlazor v9 source to:

`D:\Repositories\References\MudBlazor-9.0.0`

This path is hardcoded in the script. Users can edit the script constant when needed.

## MudBlazor v9 Source-First Rules

1. Treat local MudBlazor source as the source of truth.
2. If any API or behavior is uncertain, search the source first, not old migration notes.
3. Preferred source entry points:
   - `src/MudBlazor/Components/...`
   - `src/MudBlazor/Styles/...`
   - `src/MudBlazor.UnitTests/...`
4. Use `rg` for quick lookup:

```bash
rg -n "ShowAsync|ShowMessageBoxAsync|GetDefaultConverter|IReversibleConverter" D:\Repositories\References\MudBlazor-9.0.0\src
```

## Critical UI Rules

### 1. CSS Isolation

- Never use `<style>` tags in `.razor`.
- Use `.razor.css` files.
- CSS isolation applies to HTML elements, not Razor components.
- For MudBlazor styling, wrap with a container and use `::deep`.

```razor
<div class="table-wrapper">
    <MudTable Items="@items" />
</div>
```

```css
.table-wrapper ::deep .mud-table {
    background-color: var(--mud-palette-surface);
}
```

### 2. Icons

Always use `@` for icon expressions:

```razor
<MudIconButton Icon="@Icons.Material.Filled.Add" />
```

### 3. Generic Component `T` Parameter

Always specify `T` for generic MudBlazor components:

```razor
<MudSwitch T="bool" @bind-Value="@isEnabled" />
```

### 4. Lifecycle and JS Interop

- Do not run JS interop in `OnInitializedAsync`.
- Use `OnAfterRenderAsync(firstRender)` for JS interop and heavy first-load tasks.
- Use `CancellationToken` for async loading tasks.

### 5. MudBlazor v9 Async APIs

- Use async methods only (`ShowAsync`, `ShowMessageBoxAsync`, etc.).
- Do not use removed sync APIs from older versions.

### 6. Converters and Custom Form Components

- Use v9 converter interfaces (`IConverter`, `IReversibleConverter`).
- Custom form components must implement `GetDefaultConverter()`.

### 7. Offline/Intranet Requirements

- No online font/CDN dependencies for runtime UI assets.
- Keep static resources local (`wwwroot/fonts`, local CSS/JS assets).

### 8. Layout-Owned AppBar and Viewport Height

- Keep AppBar height and remaining viewport height owned by the shell layout.
- In `MoMainLayout.razor.css`, expose `--mo-appbar-height: var(--mud-appbar-height, 64px)` on `.mo-layout`.
- AppBar and navigation components must consume the layout variable (`height: var(--mo-appbar-height)` or `height: 100%` when the parent already owns the height).
- Full-height pages must rely on the parent container with `height: 100%`, `min-height: 0`, and local overflow handling instead of `calc(100vh - 64px)`, `calc(100vh - 56px)`, or similar hardcoded offsets.
- Keep scrolling in `.mo-body-content` or the page's own scroll containers; do not move scrolling back to `body`.

## MudBlazor CSS Variable Workflow (Required)

### A. Initialize or Update Variable List

Run this after MudBlazor source changes or before CSS validation:

```bash
python scripts/sync_mud_css_variables.py
```

This script reads:

`src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs`

and updates:

- `references/mudblazor-css-variables.json` (authoritative machine-readable list of real variables)

### B. Validate CSS/Razor Usage

Validate all CSS and Razor files under a project/repo root:

```bash
python scripts/validate_mud_css_variables.py --root D:\Code\MoLibrary
```

JSON output:

```bash
python scripts/validate_mud_css_variables.py --root D:\Code\MoLibrary --json
```

### C. Safe Auto-Fix Mode

Apply safe deterministic replacements, then revalidate:

```bash
python scripts/validate_mud_css_variables.py --root D:\Code\MoLibrary --fix
```

Safe auto-fix scope is intentionally limited. Remaining unknown variables require manual review.

## Browser Storage (`IMoBrowserStorage`)

- Use `IMoBrowserStorage` instead of raw `IJSRuntime` for local/session storage access.
- Load persisted UI state in `OnAfterRenderAsync(firstRender)` to avoid flash/reset issues.
- Use `BrowserStorageExtensions` for table state patterns.

See:

`references/browser-storage-guide.md`

## Localization (i18n)

- Do not hardcode user-facing text.
- Use decentralized module resources with marker class + JSON resource files.
- Keep `zh-CN.json` and `en-US.json` synchronized.
- For page content, use the module-local resource marker and JSON files.
- For `RegisterLocalizedComponent(...)` navigation/AppBar text, `displayNameKey` and `categoryKey` must exist in `Monica.UI/Localization/UIRegistryResource/*.json`, because the UI registry resolves them with `IStringLocalizer<UIRegistryResource>`.
- When adding a new page to navigation, add the corresponding `Pages:*:Title` key to `UIRegistryResource` in addition to the page module resource when needed.

Validation command:

```bash
python scripts/validate_localization.py
```

See:

`references/localization-guide.md`

## Service Error Handling in Components

For `Res/Res<T>` usage and `IsFailed` pattern in UI service calls, use the `mo-development` skill.

## References

- `references/module-structure-guide.md`
- `references/blazor-best-practices.md`
- `references/component-reference.md`
- `references/migration-guide-v9.md`
- `references/css-isolation-fix-workflow.md`
- `references/theme-css-guide.md`
- `references/offline-requirements.md`
- `references/browser-storage-guide.md`
- `references/localization-guide.md`
- `references/mudblazor-css-variables.json` (real available CSS variable list, generated)
- `references/mudblazor-css-variables.md` (semantic usage guide, manually maintained)

## Scripts

- `scripts/check_mudblazor_source.py` - Verify local MudBlazor source path (Windows/WSL compatible path resolution).
- `scripts/sync_mud_css_variables.py` - Initialize/update real MudBlazor CSS variable JSON from source.
- `scripts/validate_mud_css_variables.py` - Validate MudBlazor variable usage in CSS/Razor files and apply safe auto-fixes.
- `scripts/validate_localization.py` - Validate localization keys (missing/unused/sync) and verify `RegisterLocalizedComponent` keys against `UIRegistryResource`.
- `scripts/font_downloader.py` - Download fonts for offline usage.

## Quick Checklist

- [ ] Run source check script first
- [ ] Confirm uncertain APIs from MudBlazor source
- [ ] Use CSS isolation (`.razor.css`) with wrapper + `::deep`
- [ ] Use MudBlazor v9 async APIs
- [ ] Use valid MudBlazor CSS variables only
- [ ] Run CSS variable validation when styling changes
- [ ] Use `IMoBrowserStorage` for browser persistence
- [ ] Keep AppBar height and viewport compensation in the shell layout, not in page CSS
- [ ] Use localization for all user-facing text
- [ ] Add AppBar/navigation keys to `UIRegistryResource` when using `RegisterLocalizedComponent`
