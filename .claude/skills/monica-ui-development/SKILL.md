---
name: monica-ui-development
description: This skill should be used when the user asks to create or modify Blazor UI components, build MudBlazor pages, style MudBlazor components, fix CSS isolation, customize themes, migrate to MudBlazor v9, validate MudBlazor CSS variables, or implement browser storage with IBrowserStorage.
version: 2.13.0
---

# Monica UI Development Guide

This skill is for Monica Blazor UI work with MudBlazor v9.

All script paths in this document are relative to the `monica-ui-development` skill directory.

Project-local temporary state for this skill is stored under:

- `.tmp/monica-ui-development/mudblazor-css-variables.json` - generated machine-readable CSS variable list

## Companion Audit Requirement

When creating or modifying Monica UI components or pages, also use `monica-ui-audit` as a companion guardrail.

- For focused component/page edits, run a targeted P0-P3 audit before changing files and let the findings shape the fix.
- For broad UI scopes, report the audit findings first, then fix the agreed items in priority order.
- Treat Rule #9 layout issues as audit-relevant even when there is no P0/P1 violation: missing local spacing, overflow control, centering, or shell structure belongs in the owning component `.razor.css`.
- Do not move component-specific layout into theme CSS just to make the audit pass; theme files own global MudBlazor visuals, not page-local layout.

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

This check invokes the user-level `$inspect-dependency-source` skill with `resolve MudBlazor --json`. The global catalog is shared across projects and agent CLIs; Monica does not read its storage directly or maintain a project-local source-path config.

The resolver CLI is discovered in this order:

1. `INSPECT_DEPENDENCY_SOURCE_CLI`
2. `$HOME/.agents/skills/inspect-dependency-source/scripts/inspect_dependency_source.py`
3. `$HOME/.claude/skills/inspect-dependency-source/scripts/inspect_dependency_source.py`

If the current task is source-dependent and the check fails, you must **stop that work immediately**. Do not continue by guessing from memory, migration notes, or outdated examples.

Required recovery flow for source-dependent work:

1. Install the user-level `$inspect-dependency-source` skill if it is unavailable.
2. Register MudBlazor source in its shared global catalog.
3. Only continue after `python scripts/check_mudblazor_source.py` succeeds.

Typical registration commands:

```bash
python3 "${INSPECT_DEPENDENCY_SOURCE_CLI:-$HOME/.agents/skills/inspect-dependency-source/scripts/inspect_dependency_source.py}" repo add-local <mudblazor-source-root> --alias MudBlazor
```

If the task is not source-dependent and the existing references are enough, continue without source inspection.

## MudBlazor v9 Source-First Rules

1. For any source-dependent UI task, MudBlazor source availability is mandatory.
2. Treat local MudBlazor source as the source of truth for uncertain APIs or behavior.
3. If source is unavailable, stop the source-dependent task until MudBlazor is registered in the user-level `$inspect-dependency-source` catalog.
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
- Do not assume a class added to a Razor component such as `MudPaper`, `MudGrid`, `MudTabs`, `MudStack`, or `MudContainer` can be styled by a plain isolated selector like `.my-class { ... }`.
- A class on a rendered MudBlazor root can appear in the live DOM while still missing the component's Blazor scope attribute, so isolated selectors compiled to `.my-class[b-xxxx]` will not match.
- If you need to style a MudBlazor component root or internal structure, put the scope on a real HTML wrapper and target the MudBlazor element with `::deep`.
- If a class is visible in HTML but computed styles remain at Mud defaults, inspect the emitted `*.bundle.scp.css` and compare the compiled selector against the actual runtime DOM before changing layout code.
- Validate actual runtime MudBlazor DOM class names before writing selectors. Do not guess names such as `toolbar` vs `tabbar`.

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

Anti-pattern:

```razor
<MudPaper Class="my-card" />
```

```css
.my-card {
    padding: 1rem;
}
```

Preferred pattern:

```razor
<div class="card-wrapper">
    <MudPaper Class="my-card" />
</div>
```

```css
.card-wrapper ::deep .my-card {
    padding: 1rem;
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
- For page-owned auto-refresh, prefer the `PeriodicTimer` pattern in `references/auto-refresh-page-pattern.md` over `System.Timers.Timer`.

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

### 9. Theme-First Visual Simplicity and Component Responsibility

- Prefer simple, quiet layouts that mostly rely on the active MudBlazor theme.
- Do not introduce gradients, glow effects, decorative shadows, or custom multi-color surfaces unless the user explicitly asks for a branded visual treatment.
- Favor `var(--mud-palette-surface)`, `var(--mud-palette-background-gray)`, `var(--mud-palette-lines-default)`, and `var(--mud-palette-text-secondary)` over inventing new color systems.
- Use CSS isolation primarily for layout, spacing, centering, sizing, and overflow control. Do not use it to repaint large parts of MudBlazor unless there is a clear product requirement.
- When list or card UIs become dense, remove redundant metadata first. Prefer a minimal primary view and move secondary details into dialogs, drawers, or detail panes.
- If centered alignment looks wrong, fix the container layout first (`display`, `align-items`, `justify-content`, `min-height`, `min-width`) before adding margin or padding hacks.
- Cards that visually belong to the same row should generally align to the same height. Prefer row-level grid/flex stretch plus wrapper-owned `height: 100%` over fixed pixel heights.

**Component CSS responsibility model:**

| Layer | Owns | Does NOT own |
|-------|------|----|
| MudTheme (C#) | Palette tokens, typography | Component-specific visuals |
| Theme CSS (`themes/*.css`) | Visual language on **standard MudBlazor selectors** | Layout, positioning |
| Shared layout CSS (`mo-theme-main.css`) | Cross-module utilities and app-shell/global layout contracts | Component-specific layout or presentation |
| Component CSS (`.razor.css`) | Component-specific layout, sizing, positioning, responsive rules, truncation, localized token-based presentation | Hardcoded colors, theme variants, global MudBlazor behavior |

Key rules:
- **Never** create private component classes (e.g., `.dropdown-menu`, `.flyout-menu`) that themes must discover and target. Private component classes are acceptable when they stay inside the owning `.razor` and `.razor.css` files.
- **Always** use MudBlazor primitives for interactive patterns (menus, dialogs, overlays). See Rule #10.
- When MudBlazor component parameters are insufficient, use component isolation CSS for the owning component's layout. Promote hooks to `mo-theme-main.css` only for shared layout utilities used across multiple modules.
- Component CSS may provide functional defaults using CSS variables (e.g., `.navbar-link` hover using `var(--mud-palette-primary)`); themes override these via higher specificity on shared selectors.

### 10. Use MudBlazor Primitives for Interactive UI

- All menu and dropdown patterns must use `MudMenu` + `MudMenuItem`. Do not build custom dropdown markup with manual hover tracking, delayed-close state machines, or pointer event handlers.
- All overlay patterns must use `MudDialog`, `MudDrawer`, or `MudPopover`. Do not build custom flyout panels.
- Active route state in menus: apply an `.active` CSS class via `NavigationRouteMatcher.IsActive()` and let theme CSS style `.mud-menu-item.active`. Do not paint active state in component CSS.
- When `MudMenu` built-in parameters are insufficient for layout, pass sizing/scrolling classes through `PopoverClass` or `ListClass`. Keep component-specific hooks in the owning `.razor.css`; use `mo-theme-main.css` only for shared layout utilities used by multiple modules.
- Reference implementation: `NavBarDropdown.razor` and `NavBarMore.razor` in `Monica.UI/Shell/Components/Layout/`.

### 11. Theme Authoring and Verification

- Put global theme visuals in shared theme CSS under `Monica.UI/wwwroot/css/themes/`. Keep component-specific layout and localized token-based presentation in `.razor.css`.
- First-party Monica UI colors must use `--mud-palette-*` first, or the small supplemental `--mo-color-*` contract from `Monica.UI/wwwroot/css/mo-theme-main.css` when MudBlazor palette roles are not expressive enough.
- Do not consume private theme namespaces such as `--mo-m3-*`, `--mo-ink-*`, `--mo-hermes-*`, `--mo-fresh-*`, `--mo-vibe-*`, or `--mo-zen-*` from component/page code.
- Do not introduce shared component styling in `mo-theme-main.css` just because a color token exists; component layout and presentation selectors stay in the owning component/page CSS unless there is a separate shared-layout requirement.
- Do not create page-specific color aliases when an approved semantic token already covers the scenario.
- Runtime visualization payloads from C#, Razor, or JS must emit `var(--mud-palette-*)` or approved `var(--mo-color-*)` values instead of raw hex, rgb, or hsl strings.
- Prefer shared MudBlazor selectors over page-only hooks. If you add a temporary page-specific class during diagnosis, remove it after the shared theme rule is in place.
- For `MudTabs` with `ApplyEffectsToContainer="true"`, the root `.mud-tabs` element receives the rounded, outlined, and elevation classes. When a theme needs a visible shell, inspect and style the root container, `.mud-tabs-tabbar`, and `.mud-tabs-panels` together.
- `MudDataGrid` header affordances are hover-hidden by default in MudBlazor. If a custom theme makes headers look blank, inspect and style `.sort-direction-icon`, `.column-options-icon`, `.drag-icon-options`, and `.mud-menu .mud-icon-button-label`.
- Debug theme regressions with live DOM and computed-style checks before editing CSS. Verify both light and dark modes and inspect MudBlazor source when component behavior is uncertain.
- Read `references/theme-authoring-pitfalls.md` when working on shared theme regressions or resuming a theme-debugging thread. That file carries the concrete regression patterns and verification traps.
- Read `references/adaptive-mudblazor-list-table-pattern.md` when fixing dense MudBlazor list/table overflow, adaptive ellipsis, or CSS-grid table alignment issues.

Validate semantic theme-token compliance with:

```bash
python <project-root>/scripts/validate_ui_theme_tokens.py
```

## MudBlazor CSS Variable Workflow (Required)

### A. Initialize or Update Variable List

Run this when you need to refresh the generated variable list from MudBlazor source:

```bash
python scripts/sync_mud_css_variables.py
```

This workflow is source-dependent. If `python scripts/check_mudblazor_source.py` cannot resolve MudBlazor through `$inspect-dependency-source`, stop and follow the source recovery flow above.

This script reads:

`src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs`

and updates:

- `.tmp/monica-ui-development/mudblazor-css-variables.json` (authoritative machine-readable list of real variables)

### B. Validate CSS/Razor Usage

Validate all CSS and Razor files under a project/repo root:

```bash
python scripts/validate_mud_css_variables.py --root <project-root>
```

JSON output:

```bash
python scripts/validate_mud_css_variables.py --root <project-root> --json
```

### C. Safe Auto-Fix Mode

Apply safe deterministic replacements, then revalidate:

```bash
python scripts/validate_mud_css_variables.py --root <project-root> --fix
```

Safe auto-fix scope is intentionally limited. Remaining unknown variables require manual review.

## Browser Storage (`IBrowserStorage`)

- Use `IBrowserStorage` instead of raw `IJSRuntime` for local/session storage access.
- Load persisted UI state in `OnAfterRenderAsync(firstRender)` to avoid flash/reset issues.
- Use `BrowserStorageExtensions` for table state patterns.
- When you manually verify persisted theme behavior in Playwright or browser DevTools, remember that the runtime storage key is `mo:theme:data` because `IBrowserStorage` auto-prefixes keys with `mo:`.

See:

`references/browser-storage-guide.md`

## Localization (i18n)

For any Monica UI localization/i18n work, also use `$monica-ui-localization`. That skill owns resource structure, `IStringLocalizer<TResource>` usage, UI registry keys, language synchronization, and the strict validation workflow.

## Service Error Handling in Components

For `Res/Res<T>` usage, `IResultEnvelope`, and the `IsFailed` pattern in UI service calls, use the `monica-development` skill.

## References

- `references/module-structure-guide.md`
- `references/blazor-best-practices.md`
- `references/component-reference.md`
- `references/migration-guide-v9.md`
- `references/css-isolation-fix-workflow.md`
- `references/theme-css-guide.md`
- `references/theme-authoring-pitfalls.md`
- `references/auto-refresh-page-pattern.md`
- `references/browser-storage-guide.md`
- `references/offline-requirements.md`
- `.tmp/monica-ui-development/mudblazor-css-variables.json` (real available CSS variable list, generated)
- `references/mudblazor-css-variables.md` (semantic usage guide, manually maintained)
- `$inspect-dependency-source` (user-level shared source catalog; access it only through the CLI contract)

## Scripts

- `scripts/check_mudblazor_source.py` - Invoke `inspect-dependency-source resolve MudBlazor --json`, validate the returned path, and verify that the required source marker exists.
- `scripts/sync_mud_css_variables.py` - Initialize/update real MudBlazor CSS variable JSON into `.tmp/monica-ui-development/mudblazor-css-variables.json`.
- `scripts/validate_mud_css_variables.py` - Validate MudBlazor variable usage in CSS/Razor files and apply safe auto-fixes using the generated `.tmp` variable list by default.
- `scripts/font_downloader.py` - Download fonts for offline usage.

## Quick Checklist

- [ ] Run the source check only for source-dependent work
- [ ] If source check fails during source-dependent work, stop and register MudBlazor source through the user-level `$inspect-dependency-source` skill
- [ ] Confirm uncertain APIs from MudBlazor source before continuing source-dependent work
- [ ] Use `monica-ui-audit` as a companion check for Monica UI component/page changes
- [ ] Use CSS isolation (`.razor.css`) with wrapper + `::deep`
- [ ] Use MudBlazor v9 async APIs
- [ ] Use valid MudBlazor CSS variables only
- [ ] Run CSS variable validation when styling changes
- [ ] Use `IBrowserStorage` for browser persistence
- [ ] Keep AppBar height and viewport compensation in the shell layout, not in page CSS
- [ ] Use `$monica-ui-localization` for any user-facing text or i18n resource changes

## Page Complexity Checklist

Before creating or modifying a page, verify it stays within architecture limits. See `monica-architecture` skill for full Page Decomposition Rules.

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

For a real anti-pattern case study (`UIAIRAGManagePage.razor` — 1,614 lines), see `monica-architecture` skill → `references/refactoring-examples.md`.

For State class implementation patterns (data bag, async Facade-calling, polling/concurrency), see `monica-architecture` skill → `references/page-state-pattern.md`.
