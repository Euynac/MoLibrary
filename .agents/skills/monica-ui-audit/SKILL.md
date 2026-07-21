---
name: monica-ui-audit
description: Audit and fix Monica Blazor UI compliance, including visual hierarchy, theme-token use, MudBlazor primitives, responsive layout containment, dialog or container width, overflow ownership, and duplicate utility logic. Use when the user asks to "audit UI components", "check theme compliance", "find CSS violations", "review component styling", "theme-first audit", "UI规约检查", "组件合规", or reports that a UI is bland, monotonous, lacks meaningful color differentiation, clips content, wastes width, or has broken responsive sizing.
---

# Theme-First UI Compliance Audit

Audit Blazor components for violations of the Monica UI theme-first rules defined in `monica-ui-development` SKILL.md Rules #9 and #10. Theme-first means coherent and token-governed, not minimal, monochrome, or visually flat.

## How to Determine Audit Target

1. If the user specifies a file or directory, audit that target.
2. If the user says "all" or gives no target, scan `Monica.UI/Shell/Components/` and `Monica.UI/Shell/Pages/` directories.
3. For a single component, read its `.razor`, `.razor.cs`, and `.razor.css` files together.

## Violation Categories

Scan for these violations in order of severity:

### P0 — Custom interactive markup replacing MudBlazor primitives

Indicators:
- Custom dropdown/flyout/overlay markup with manual `@onmouseenter` / `@onmouseleave` / `@onclick` state machines
- Manual `_isOpen`, `_isHovered`, delayed-close `CancellationTokenSource` patterns
- Custom `<div class="dropdown-menu">` / `<div class="flyout-menu">` / `<div class="popover-panel">` instead of `MudMenu`, `MudPopover`, `MudDialog`, `MudDrawer`

Fix: replace with MudBlazor primitives. Reference implementation: `NavBarDropdown.razor`, `NavBarMore.razor` in `Monica.UI/Shell/Components/Layout/`.

### P1 — Broken layout containment, sizing, or scroll ownership

Indicators:
- A `FullWidth` dialog, drawer, tab panel, or preview has a descendant `width`, `max-width`, grid track, or intrinsic `inline`/`fit-content` size that leaves usable space empty
- A flex/grid child that must shrink omits `min-width: 0`, or a grid uses `1fr` where `minmax(0, 1fr)` is required
- A component-isolated selector targets a MudBlazor render root it cannot reach; use a scoped native wrapper or a reachable `::deep` descendant selector
- Long hashes, identifiers, JSON, tables, or code stretch an ancestor instead of wrapping or scrolling inside the intended local surface
- The page or dialog becomes the horizontal scroll owner when only a table, diff, or code surface should scroll
- A fixed/minimum height leaves unexplained blank space instead of using content-driven height with a viewport-aware cap

Fix: trace the layout from the dialog/page surface to the failing descendant and assign width, shrink, wrap, and overflow responsibility explicitly. Remove contradictory internal width caps, use `width: 100%`, `min-width: 0`, `minmax(0, 1fr)`, `overflow-wrap`, or a local scroll surface only where each property expresses the intended contract. Keep long identifiers fully accessible for inspection and copying. Justify every retained width cap or fixed/minimum height with a concrete readability, interaction, or viewport requirement.

### P2 — Theme bypass or insufficient visual differentiation

Indicators:
- Inline `Style=` / `style=` attributes for layout or visuals
- Hardcoded color, shadow, radius, or background values instead of MudBlazor parameters, `var(--mud-palette-*)`, or approved `var(--mo-color-*)` tokens
- Semantically distinct statuses, severities, categories, or progress states collapse into visually identical neutral surfaces when scanability requires differentiation
- Multiple hierarchy levels and semantic states rely on the same neutral surface plus one accent, producing a flat or monotonous page
- Component CSS repainting global MudBlazor behavior that should be consistent across modules
- Component CSS painting route `.active`, `:hover`, or `:focus` states for menu items, nav links, or list items that are themed globally
- Dark-mode overrides inside component CSS (`[data-theme*="dark"]` in `.razor.css`)

Fix: add purposeful hierarchy and semantic variation with MudBlazor parameters and approved theme tokens (`var(--mud-palette-*)`, `var(--mo-color-*)`, `Color`, `Variant`, `Elevation`, `Outlined`). Keep component-owned layout and token-based presentation in the component's `.razor.css` file. Move rules to theme CSS only when the rule intentionally changes global MudBlazor/theme behavior across modules.

### P3 — Private component classes that force theme coupling

Indicators:
- Custom CSS classes on menu surfaces, row items, or overlay panels (e.g. `.my-dropdown`, `.custom-flyout-item`) that are also targeted in theme files
- Theme files containing selectors for these private component classes
- Grep theme files for the private class name to confirm coupling

Fix: remove theme coupling. If the class is only used by the owning component, keep it in the component's `.razor.css`. Promote a hook to shared CSS only when multiple components intentionally share the same layout contract.

### P4 — Duplicate utility logic

Indicators:
- Text-resolution helpers like `GetItemText(NavigationItem)` duplicated across components instead of using `NavigationItem.ResolveDisplayText(localizer)`
- Manual route-matching wrappers instead of `NavigationRouteMatcher.GetActiveClass()`
- Repeated localization key lookup patterns that could use shared model methods

Fix: use the shared utilities in `Monica.UI/Shell/Support/` and methods on `Monica.UI/Shell/Models/NavigationItem`.

## Audit Workflow

1. **Scan**: Read all `.razor`, `.razor.cs`, and `.razor.css` files in the target scope.
2. **Trace containment**: For every dialog, drawer, grid, split view, table, or code/diff surface in scope, follow every ancestor from the outer surface to the content. Check width/max-width, flex/grid shrinkability, rendered selector reachability under CSS isolation, long-token behavior, height constraints, and which element owns each scrollbar. When selector reachability is suspect, verify the target's computed style or compare the compiled isolated selector with the rendered DOM; do not infer success from a class name alone.
3. **Detect**: For each violation, note the file path, line range, category (P0–P4), evidence, and the component that should own the fix.
4. **Report**: Present the report template below before making changes. Ask the user to confirm which violations to fix if the scope is large.
5. **Fix**: Apply concrete fixes, preserving all existing functionality, localization, and responsive behavior.
6. **Clean up**: Delete any `.razor.css` file only when the component no longer owns layout or localized presentation rules. Remove unused `@using` directives.
7. **Build**: Build the affected project with the WSL Windows-path rule, for example `dotnet build '<windows-project-path>' -m` for the specific UI module project. The build must produce 0 warnings.
8. **Verify in browser**: Test representative desktop and narrow viewports. For the document, dialog surface, and every element not intentionally designated as a local horizontal scroller, assert `scrollWidth <= clientWidth`. For every surface intended to fill its owner, also compare its rendered width with the owner's available content width; an overflow-free half-width child is still a failure. Record the intended local scroll owner, keep wrapped or truncated identifiers inspectable/copyable, and exercise content-driven panes with both short and long data when feasible so they neither retain unexplained blank height nor grow without a viewport cap.
9. **Report**: List what changed, browser assertions, any intentional local scrolling, theme files that may need a shared selector, and remaining manual verification.

## Report Template

| Priority | Location | Evidence | Correct owner/fix | Verification |
|---|---|---|---|---|
| P0–P4 | File and line | Rendered or source-level failure | Component/theme/layout owner | Build and browser assertion |

Include desktop and narrow results for `scrollWidth <= clientWidth`, rendered-width utilization for intended full-width surfaces, every intentional local horizontal scroller, and the justification for each retained width cap or fixed/minimum height.

## Constraints

- Do NOT add hardcoded visual styling to component CSS. Component isolation CSS may own component-specific layout, overflow, sizing, truncation, and localized presentation that uses MudBlazor theme tokens.
- Do NOT reject purposeful token-based color, elevation, gradients, borders, or atmosphere merely because a more minimal treatment exists.
- Do NOT break existing responsive or compact-mode behavior.
- Do NOT treat `overflow-x: hidden` on the page or dialog as a containment fix; repair the child width contract and keep scrolling on the smallest surface that needs it.
- Layout-only hooks (sizing, scroll, positioning, truncation) should usually stay in the component's `.razor.css`. Use `mo-*` shared CSS only for truly shared layout utilities used across multiple modules.
- Active route state: use `.active` class via `NavigationRouteMatcher.GetActiveClass()`, styled by themes on `.mud-menu-item.active`.
- Follow `monica-ui-development` SKILL.md Rule #1 (CSS Isolation), Rule #9 (Theme-First Visual Richness and Component Responsibility), and Rule #10 (Use MudBlazor Primitives for Interactive UI) as the authoritative references.

## Component CSS Responsibility Reference

| Layer | Owns | Does NOT own |
|-------|------|----|
| MudTheme (C#) | Palette tokens, typography | Component-specific visuals |
| Theme CSS (`themes/*.css`) | Visual language on standard MudBlazor selectors | Layout, positioning |
| Shared layout CSS (`mo-theme-main.css`) | Cross-module utilities and app-shell/global layout contracts | Component-specific layout or presentation |
| Component CSS (`.razor.css`) | Component-specific layout, sizing, positioning, responsive rules, truncation, localized token-based presentation | Hardcoded colors, theme variants, global MudBlazor behavior |
