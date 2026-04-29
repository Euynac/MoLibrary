---
name: mo-ui-audit
description: This skill should be used when the user asks to "audit UI components", "check theme compliance", "find CSS violations", "review component styling", "theme-first audit", "UI规约检查", "组件合规", or wants to find and fix Blazor components that violate Monica UI rules (custom dropdowns instead of MudBlazor primitives, inline or hardcoded styling, theme coupling, duplicate utility logic).
version: 1.0.0
---

# Theme-First Component Compliance Audit

Audit Blazor components for violations of the Monica UI theme-first rules defined in `mo-ui-development` SKILL.md Rules #9 and #10.

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

### P1 — Inline or hardcoded styling that bypasses the theme

Indicators:
- Inline `Style=` / `style=` attributes for layout or visuals
- Hardcoded color, shadow, radius, or background values instead of MudBlazor parameters or `var(--mud-palette-*)` tokens
- Component CSS repainting global MudBlazor behavior that should be consistent across modules
- Component CSS painting route `.active`, `:hover`, or `:focus` states for menu items, nav links, or list items that are themed globally
- Dark-mode overrides inside component CSS (`[data-theme*="dark"]` in `.razor.css`)

Fix: keep component-owned layout and localized presentation in the component's `.razor.css` file. Use MudBlazor parameters and theme tokens (`var(--mud-palette-*)`, spacing classes, `Color`, `Variant`, `Elevation`, `Outlined`) instead of hardcoded colors or inline styles. Move rules to theme CSS only when the rule intentionally changes a global MudBlazor/theme behavior across modules.

### P2 — Private component classes that force theme coupling

Indicators:
- Custom CSS classes on menu surfaces, row items, or overlay panels (e.g. `.my-dropdown`, `.custom-flyout-item`) that are also targeted in theme files
- Theme files containing selectors for these private component classes
- Grep theme files for the private class name to confirm coupling

Fix: remove theme coupling. If the class is only used by the owning component, keep it in the component's `.razor.css`. Promote a hook to shared CSS only when multiple components intentionally share the same layout contract.

### P3 — Duplicate utility logic

Indicators:
- Text-resolution helpers like `GetItemText(NavigationItem)` duplicated across components instead of using `NavigationItem.ResolveDisplayText(localizer)`
- Manual route-matching wrappers instead of `NavigationRouteMatcher.GetActiveClass()`
- Repeated localization key lookup patterns that could use shared model methods

Fix: use the shared utilities in `Monica.UI/Shell/Support/` and methods on `Monica.UI/Shell/Models/NavigationItem`.

## Audit Workflow

1. **Scan**: Read all `.razor`, `.razor.cs`, and `.razor.css` files in the target scope.
2. **Detect**: For each violation found, note the file path, line range, category (P0–P3), and the specific pattern that violates.
3. **Report**: Present a summary table of violations before making changes. Ask the user to confirm which violations to fix if the scope is large.
4. **Fix**: Apply concrete fixes, preserving all existing functionality, localization, and responsive behavior.
5. **Clean up**: Delete any `.razor.css` file only when the component no longer owns layout or localized presentation rules. Remove unused `@using` directives.
6. **Build**: Build the affected project with the WSL Windows-path rule, for example `dotnet build '<windows-project-path>' -m` for the specific UI module project. The build must produce 0 warnings.
7. **Report**: List what was changed, which theme files may need a new shared selector, and any remaining items that need manual UI verification.

## Constraints

- Do NOT add hardcoded visual styling to component CSS. Component isolation CSS may own component-specific layout, overflow, sizing, truncation, and localized presentation that uses MudBlazor theme tokens.
- Do NOT break existing responsive or compact-mode behavior.
- Layout-only hooks (sizing, scroll, positioning, truncation) should usually stay in the component's `.razor.css`. Use `mo-*` shared CSS only for truly shared layout utilities used across multiple modules.
- Active route state: use `.active` class via `NavigationRouteMatcher.GetActiveClass()`, styled by themes on `.mud-menu-item.active`.
- Follow `mo-ui-development` SKILL.md Rules #9 (Theme-First Visual Simplicity and Component Responsibility) and #10 (Use MudBlazor Primitives for Interactive UI) as the authoritative reference.

## Component CSS Responsibility Reference

| Layer | Owns | Does NOT own |
|-------|------|----|
| MudTheme (C#) | Palette tokens, typography | Component-specific visuals |
| Theme CSS (`themes/*.css`) | Visual language on standard MudBlazor selectors | Layout, positioning |
| Shared layout CSS (`mo-theme-main.css`) | Cross-module utilities and app-shell/global layout contracts | Component-specific layout or presentation |
| Component CSS (`.razor.css`) | Component-specific layout, sizing, positioning, responsive rules, truncation, localized token-based presentation | Hardcoded colors, theme variants, global MudBlazor behavior |
