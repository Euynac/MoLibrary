---
name: monica-ui-development
description: Create or modify Monica Blazor UI components and MudBlazor pages, including CSS isolation, global/default themes, prototype handoff, local WOFF2 fonts, MudBlazor v9 migration, CSS-variable validation, and IBrowserStorage. Use for UI implementation, visual-language changes, theme customization, or browser-storage work.
---

# Monica UI Development Guide

This skill is for Monica Blazor UI work with MudBlazor v9.

All script paths in this document are relative to the `monica-ui-development` skill directory.

Project-local temporary state for this skill is stored under:

- `.tmp/monica-ui-development/mudblazor-css-variables.json` - generated machine-readable CSS variable list

## Companion Audit Requirement

When creating or modifying Monica UI components or pages, also use `monica-ui-audit` as a companion guardrail.

- For focused component/page edits, run a targeted P0-P4 audit before changing files and let the findings shape the fix.
- For broad UI scopes, report the audit findings first, then fix the agreed items in priority order.
- Treat Rule #9 page-local layout issues as audit-relevant even when they do not rise to a P2 containment violation: missing local spacing, overflow control, centering, or shell structure belongs in the owning component `.razor.css`.
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

This check invokes the user-level `$inspect-dependency-source` skill with `resolve MudBlazor --ref 9.0.0 --json`. The exact ref matches Monica.UI's MudBlazor dependency and prevents another cached MudBlazor checkout from being selected. The global catalog is shared across projects and agent CLIs; Monica does not read its storage directly or maintain a project-local source-path config.

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
- Use `OnInitializedAsync` or `OnParametersSetAsync` for data initialization and leave the component in a valid renderable state before each await. Reserve `OnAfterRenderAsync` for DOM-dependent work that genuinely requires rendered elements; do not turn it into a general first-load orchestrator.
- Treat every incomplete `await` as a re-entrancy boundary. Component disposal or another lifecycle/event callback may run before the continuation resumes, including inside `OnAfterRenderAsync`.
- Give long-lived component/page-state work a component-lifetime `CancellationTokenSource`. Cancel it at the start of disposal, pass its token to calls whose cancellation semantics cannot orphan an owned resource, and check cancellation/disposal after awaited operations that cannot accept the token. Always observe resource-producing operations such as JS module imports so a late-created result can be disposed instead of leaked.
- A non-null `IJSObjectReference` is not proof that the reference is usable. Before teardown, prevent new acquisitions and serialize with or drain operations that already captured the reference; only then atomically detach and dispose it. Do not leave disposed references published to render/event continuations.
- If an async module import or initialization completes after the owner is disposed, dispose the newly created reference instead of assigning it to component state.
- Track and observe every background task. Navigation callbacks, timers, and event handlers must not start unbounded fire-and-forget work that can render, mutate state, or use JS after disposal.
- Do not register a component-consumed disposable service as transient. DI retains disposable transients for the app/circuit scope, and manual component disposal adds conflicting ownership. Prefer a non-disposable factory that creates a component-owned session. Use an explicit component-owned scope such as `OwningComponentBase` only after inspecting the service's dependency graph: a separate scope does not automatically preserve access to services bound to the existing Blazor circuit scope.
- Retain every `DotNetObjectReference.Create(...)` result in its .NET owner and dispose it deterministically. Passing an inline reference to JavaScript does not transfer ownership by itself.
- Stop all JavaScript observers, listeners, animation callbacks, and other callback producers before disposing their `DotNetObjectReference`.
- Keep per-component JavaScript state in a page/session instance rooted by concrete `ElementReference`s. Avoid module-global active state and document-global selectors that allow an old component's teardown to affect a replacement instance, and cancel queued animation frames/timers during JS-side cleanup.
- Do not invoke JavaScript from `DisposeAsync` to clean up DOM. Use a client-side `MutationObserver`; disposing an owned `IJSObjectReference` in `DisposeAsync` is still appropriate.
- On Blazor Server, catch expected `JSDisconnectedException` around interop/module disposal when the circuit can already be gone. Do not catch `ObjectDisposedException` from a locally owned interop reference; it signals incorrect ownership or teardown ordering.
- Make disposal idempotent and fast. When initialization, invocation, and disposal can overlap, cover the interleaving with a deterministic test that blocks the async operation, starts disposal, and then releases the continuation.
- For page-owned auto-refresh, prefer the `PeriodicTimer` pattern in `references/auto-refresh-page-pattern.md` over `System.Timers.Timer`.

### 5. MudBlazor v9 Async APIs

- Use async methods only (`ShowAsync`, `ShowMessageBoxAsync`, etc.).
- Do not use removed sync APIs from older versions.

### 6. Converters and Custom Form Components

- Use v9 converter interfaces (`IConverter`, `IReversibleConverter`).
- Custom form components must implement `GetDefaultConverter()`.

### 7. Offline/Intranet Requirements

- No online font/CDN dependencies for runtime UI assets.
- Runtime font assets must be local WOFF2 files under `wwwroot/fonts`; never keep source TTF/OTF files under Monica UI package projects.
- Keep source TTF/OTF files in ignored tooling/cache paths such as `.tmp/monica-ui-font-sources/`, or pass them explicitly to tooling with `--source-font`.
- When mirroring a third-party theme, vendor the exact source font when possible, generate WOFF2 runtime files, and reference them through local `@font-face` rules.
- For CJK or other large display fonts, prefer checked WOFF2 subsets generated from localization resources over full-font runtime bundles.
- Keep all other static resources local (`wwwroot/fonts`, local CSS/JS assets).

### 8. Layout-Owned AppBar and Viewport Height

- Keep AppBar height and remaining viewport height owned by the shell layout.
- In `MoMainLayout.razor.css`, expose `--mo-appbar-height: var(--mud-appbar-height, 64px)` on `.mo-layout`.
- AppBar and navigation components must consume the layout variable (`height: var(--mo-appbar-height)` or `height: 100%` when the parent already owns the height).
- Full-height pages must rely on the parent container with `height: 100%`, `min-height: 0`, and local overflow handling instead of `calc(100vh - 64px)`, `calc(100vh - 56px)`, or similar hardcoded offsets.
- Loading, empty, and placeholder states should consume available space with flex/grid alignment when the parent height is available, instead of using large fixed top/bottom padding for visual centering.
- Keep scrolling in `.mo-body-content` or the page's own scroll containers; do not move scrolling back to `body`.

### 9. Precision Visual Language and Component Responsibility

- Read `references/monica-precision-design-language.md` before default-theme, global visual-language, or prototype-handoff work. Apply it as an acceptance contract.
- Before styling, write a subject-specific design thesis and a compact plan for palette roles, typography roles and fallbacks, layout rhythm, and one signature treatment. The plan must explain how the page's actual Monica task—not a generic admin-dashboard aesthetic—drives those choices.
- Establish hierarchy through typography, spacing, alignment, density, and explicit surface roles before adding decoration.
- Keep ordinary surfaces structurally simple and render repeated data as rows/lists/tables instead of cards. Give interactive controls, rows, and cards clear, proportionate hover, focus, pressed, and selected feedback; give noninteractive grouped content only optional token-based border, tonal, or low-shadow spatial-focus feedback that does not imply a click action.
- Use solid or tonal surfaces as the operational default. One coherent prototype-backed signature system may use a focused token-based glow, gradient, or pattern, concentrated in the identity/readiness region with limited non-competing recurrence when the prototype connects the hierarchy that way; continuous-data visualizations may use gradients that encode their scale. Keep supporting surfaces coherent rather than inert, and document either exception.
- Use short, purposeful, moderate motion that communicates interaction or state. Avoid large travel, bouncing, repeated flourishes, and continuous ambient animation; honor `prefers-reduced-motion` without removing the visible state change.
- Let dashboards, workbenches, administration pages, and diagnostics pages consume their owner's full available width. Apply readable line-length limits to text regions, not the page shell; retain a page-level `max-width` only for an explicitly editorial or reading-focused layout.
- Give light and dark palettes distinct canvas, surface, border, text, brand, and semantic roles. Do not make dark mode uniformly near-black or collapse success, warning, error, information, selection, and runtime states into one accent.
- Assign fonts by role: interface text, hierarchy/display text when needed, and diagnostics/code. Ship local WOFF2 assets, declare explicit offline-capable fallbacks (including CJK coverage where relevant), and avoid using monospace as general interface typography.
- Use the 6/8/12 radius scale without derived radius multiplication. Keep each surface within the decoration budget defined by the Precision contract.
- Let standard `MudCard` instances use the shared baseline hover. Reuse `mo-card-surface` for genuine independent native cards and its direct-child rail plus `data-mo-card-tone` for semantic KPI accents. Do not apply the hook to tables, rows, overlays, visualizations, or structural panels, and do not recreate the rail with page-private full-height borders or pseudo-elements.
- Use `var(--mud-palette-*)`, approved `var(--mo-color-*)` tokens, and MudBlazor semantic parameters for meaningful status, selection, severity, or progress—not ambient ornament.
- Keep component-specific token-based presentation and layout in component CSS. Keep the shared MudBlazor visual language in theme CSS; do not create a page-private color system.
- Fix container layout (`display`, alignment, `min-height`, `min-width`) before adding margin or padding hacks. Stretch related cards at the row level instead of assigning fixed heights.

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
- For the default theme, global visual language, or a `.ui-design` handoff, read `references/monica-precision-design-language.md` and complete its side-by-side checks at viewport widths 1440, 929, and 390 in light and dark modes.
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
- `references/monica-precision-design-language.md`
- `references/theme-authoring-pitfalls.md`
- `references/auto-refresh-page-pattern.md`
- `references/browser-storage-guide.md`
- `references/offline-requirements.md`
- `.tmp/monica-ui-development/mudblazor-css-variables.json` (real available CSS variable list, generated)
- `references/mudblazor-css-variables.md` (semantic usage guide, manually maintained)
- `$inspect-dependency-source` (user-level shared source catalog; access it only through the CLI contract)

## Scripts

- `scripts/check_mudblazor_source.py` - Invoke `inspect-dependency-source resolve MudBlazor --ref 9.0.0 --json`, validate the returned path, and verify that the required source marker exists.
- `scripts/sync_mud_css_variables.py` - Initialize/update real MudBlazor CSS variable JSON into `.tmp/monica-ui-development/mudblazor-css-variables.json`.
- `scripts/validate_mud_css_variables.py` - Validate MudBlazor variable usage in CSS/Razor files and apply safe auto-fixes using the generated `.tmp` variable list by default.
- `scripts/font_downloader.py` - Download collision-safe unicode-range WOFF2 files and generate a runtime-ready `font-faces.css` manifest. Variable `font-weight` ranges remain intact, and `--weights` selects a variable face when the requested weight falls inside its range. Select and verify required subsets such as `--subsets latin,latin-ext` instead of assuming one Google Fonts URL maps to one file.
- `scripts/subset_ui_font.py` - Generate or check localization-driven WOFF2 subsets from source fonts stored outside Monica UI packages.

## Quick Checklist

- [ ] Run the source check only for source-dependent work
- [ ] If source check fails during source-dependent work, stop and register MudBlazor source through the user-level `$inspect-dependency-source` skill
- [ ] Confirm uncertain APIs from MudBlazor source before continuing source-dependent work
- [ ] Use `monica-ui-audit` as a companion check for Monica UI component/page changes
- [ ] Use CSS isolation (`.razor.css`) with wrapper + `::deep`
- [ ] Use MudBlazor v9 async APIs
- [ ] Use valid MudBlazor CSS variables only
- [ ] Run CSS variable validation when styling changes
- [ ] Keep runtime fonts local, WOFF2-only, and referenced through local `@font-face`
- [ ] Keep source TTF/OTF files outside package projects and regenerate/check subsets after localization text changes
- [ ] Use `IBrowserStorage` for browser persistence
- [ ] Keep AppBar height and viewport compensation in the shell layout, not in page CSS
- [ ] Use `$monica-ui-localization` for any user-facing text or i18n resource changes
- [ ] Record the subject-specific design thesis, palette/type/layout plan, and one justified signature treatment
- [ ] Apply the Precision hierarchy, full-width operational-shell, responsive-surface, radius, decoration, data-density, and offline-font rules
- [ ] Verify hover, focus, pressed, and selected feedback plus `prefers-reduced-motion` behavior
- [ ] Self-critique the result for generic AI-dashboard styling before handoff
- [ ] Compare prototype and browser side by side at widths 1440, 929, and 390 in light and dark modes

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
