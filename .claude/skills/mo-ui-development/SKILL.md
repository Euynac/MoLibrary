---
name: mo-ui-development
description: This skill should be used when the user asks to create or modify Blazor UI components, build MudBlazor pages, style MudBlazor components, fix CSS isolation, customize themes, migrate to MudBlazor v9, validate MudBlazor CSS variables, implement browser storage with IMoBrowserStorage, or implement localization/i18n patterns in Monica UI modules.
version: 2.6.0
---

# Monica UI Development Guide

This skill is for Monica Blazor UI work with MudBlazor v9.

All script paths in this document are relative to the `mo-ui-development` skill directory.

Project-local temporary state for this skill is stored under:

- `.tmp/mo-ui-development/mudblazor-css-variables.json` - generated machine-readable CSS variable list

## MudBlazor Source Access (Use Only When Needed)

MudBlazor source inspection is **not required for every UI task**. Use it when:

- MudBlazor API usage or runtime behavior is uncertain
- You need to inspect component internals, styles, or unit tests
- You are verifying migration details for MudBlazor v9
- You need to refresh the authoritative CSS variable list from source

Before a source-dependent task, run:

```bash
python scripts/check_mudblazor_source.py
```

This check now resolves MudBlazor through the `third-party-source-catalog` skill. It no longer uses a separate `mo-ui-development` source-path config.

If the current task is source-dependent and the check fails, you must **stop that work immediately**. Do not continue by guessing from memory, migration notes, or outdated examples.

Required recovery flow for source-dependent work:

1. Register MudBlazor source in `third-party-source-catalog`.
2. Only continue after `python scripts/check_mudblazor_source.py` succeeds.

Typical registration commands:

```bash
python3 /mnt/d/Code/MoLibrary/.claude/skills/third-party-source-catalog/scripts/source_catalog.py local add /mnt/d/Repositories/References/MudBlazor-9.0.0
python3 /mnt/d/Code/MoLibrary/.claude/skills/third-party-source-catalog/scripts/source_catalog.py local scan /mnt/d/Repositories/References --update-existing
```

If the task is not source-dependent and the existing references are enough, continue without source inspection.

## MudBlazor v9 Source-First Rules

1. For any source-dependent UI task, MudBlazor source availability is mandatory.
2. Treat local MudBlazor source as the source of truth for uncertain APIs or behavior.
3. If source is unavailable, stop the source-dependent task until MudBlazor is registered in `third-party-source-catalog`.
4. Preferred source entry points:
   - `src/MudBlazor/Components/...`
   - `src/MudBlazor/Styles/...`
   - `src/MudBlazor.UnitTests/...`
5. Use `rg` for quick lookup after `python scripts/check_mudblazor_source.py` reports the resolved source root:

```bash
rg -n "ShowAsync|ShowMessageBoxAsync|GetDefaultConverter|IReversibleConverter" <resolved-mudblazor-source-root>/src
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
- Loading, empty, and placeholder states should consume available space with flex/grid alignment when the parent height is available, instead of using large fixed top/bottom padding for visual centering.
- Keep scrolling in `.mo-body-content` or the page's own scroll containers; do not move scrolling back to `body`.

### 9. Theme-First Visual Simplicity

- Prefer simple, quiet layouts that mostly rely on the active MudBlazor theme.
- Do not introduce gradients, glow effects, decorative shadows, or custom multi-color surfaces unless the user explicitly asks for a branded visual treatment.
- Favor `var(--mud-palette-surface)`, `var(--mud-palette-background-gray)`, `var(--mud-palette-lines-default)`, and `var(--mud-palette-text-secondary)` over inventing new color systems.
- Use CSS isolation primarily for layout, spacing, centering, sizing, and overflow control. Do not use it to repaint large parts of MudBlazor unless there is a clear product requirement.
- When list or card UIs become dense, remove redundant metadata first. Prefer a minimal primary view and move secondary details into dialogs, drawers, or detail panes.
- If centered alignment looks wrong, fix the container layout first (`display`, `align-items`, `justify-content`, `min-height`, `min-width`) before adding margin or padding hacks.

## MudBlazor CSS Variable Workflow (Required)

### A. Initialize or Update Variable List

Run this when you need to refresh the generated variable list from MudBlazor source:

```bash
python scripts/sync_mud_css_variables.py
```

This workflow is source-dependent. If `python scripts/check_mudblazor_source.py` cannot resolve MudBlazor through `third-party-source-catalog`, stop and follow the source recovery flow above.

This script reads:

`src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs`

and updates:

- `.tmp/mo-ui-development/mudblazor-css-variables.json` (authoritative machine-readable list of real variables)

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
- Keep module resource marker classes and JSON folders under the project root `Localization/` directory.
- Prefer nested JSON objects and access them with colon-separated keys such as `Page:Title` or `RuntimeConfigDialog:Intro`.
- Do not use flat dot-style keys such as `Page.Title` for new Monica UI work.
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

For `Res/Res<T>` usage, `IResultEnvelope`, and the `IsFailed` pattern in UI service calls, use the `mo-development` skill.

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
- `.tmp/mo-ui-development/mudblazor-css-variables.json` (real available CSS variable list, generated)
- `references/mudblazor-css-variables.md` (semantic usage guide, manually maintained)
- `.tmp/third-party-source-catalog/state/catalog.json` (shared source catalog consumed by the MudBlazor source check)

## Scripts

- `scripts/check_mudblazor_source.py` - Resolve MudBlazor source through `third-party-source-catalog` and verify that the required source marker exists.
- `scripts/sync_mud_css_variables.py` - Initialize/update real MudBlazor CSS variable JSON into `.tmp/mo-ui-development/mudblazor-css-variables.json`.
- `scripts/validate_mud_css_variables.py` - Validate MudBlazor variable usage in CSS/Razor files and apply safe auto-fixes using the generated `.tmp` variable list by default.
- `scripts/validate_localization.py` - Validate localization keys (missing/unused/sync) and verify `RegisterLocalizedComponent` keys against `UIRegistryResource`.
- `scripts/font_downloader.py` - Download fonts for offline usage.

## Quick Checklist

- [ ] Run the source check only for source-dependent work
- [ ] If source check fails during source-dependent work, stop and register MudBlazor source through `third-party-source-catalog`
- [ ] Confirm uncertain APIs from MudBlazor source before continuing source-dependent work
- [ ] Use CSS isolation (`.razor.css`) with wrapper + `::deep`
- [ ] Use MudBlazor v9 async APIs
- [ ] Use valid MudBlazor CSS variables only
- [ ] Run CSS variable validation when styling changes
- [ ] Use `IMoBrowserStorage` for browser persistence
- [ ] Keep AppBar height and viewport compensation in the shell layout, not in page CSS
- [ ] Use localization for all user-facing text
- [ ] Add AppBar/navigation keys to `UIRegistryResource` when using `RegisterLocalizedComponent`

## Page Complexity Checklist

Before creating or modifying a page, verify it stays within architecture limits. See `mo-architecture` skill for full Page Decomposition Rules.

### Pre-Flight Check (Before Writing a Page)

- [ ] Page markup ≤ 200 lines (composition only, no inline business logic)
- [ ] Page `@code` block ≤ 150 lines (lifecycle + event wiring)
- [ ] Total page file ≤ 350 lines
- [ ] Page injects Facades directly — no intermediate `*UIService` wrapper
- [ ] State lives in `UI{Name}/State/`, not in page fields
- [ ] No `CancellationTokenSource`, `Interlocked`, or polling loops in page code

### Red Flags During Review

| Symptom | Action |
|---------|--------|
| Page > 350 lines | Extract components to `UI{Name}/Components/` |
| > 10 private fields in `@code` | Extract state class to `UI{Name}/State/` |
| `CancellationTokenSource` in page | Move polling to `State/` or `Support/` |
| > 5 `Can*()` guard methods | Move to state class with computed properties |
| Duplicated loading skeletons | Extract shared loading component |
| Flat root `Services/` in UI module | Restructure to `UI{Name}/State/` + `UI{Name}/Support/` |
| `*UIService` wrapping a Facade | Remove wrapper, inject Facade directly |

### Correct UI Module Structure

```
Monica.{Name}.UI/
├── Pages/
│   └── UI{Name}Page.razor          # Thin shell ≤ 350 lines
├── UI{Name}/
│   ├── Components/                  # Extracted UI sections
│   ├── Dialogs/                     # Dialog components
│   ├── State/                       # Page state, polling, loading
│   └── Support/                     # Coordinators, resolvers, formatters
```

For a real anti-pattern case study (`UIAIRAGManagePage.razor` — 1,614 lines), see `mo-architecture` skill → `references/refactoring-examples.md`.

For State class implementation patterns (data bag, async Facade-calling, polling/concurrency), see `mo-architecture` skill → `references/page-state-pattern.md`.
