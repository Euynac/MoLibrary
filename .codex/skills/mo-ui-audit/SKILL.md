---
name: mo-ui-audit
description: This skill should be used when the user asks to "audit UI components", "check theme compliance", "find CSS violations", "review component styling", "theme-first audit", "UI规约检查", "组件合规", or wants to find and fix Blazor components that violate the theme-first rules (custom dropdowns instead of MudBlazor primitives, visual styling in component CSS, private classes forcing theme coupling, duplicate utility logic).
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

### P1 — Visual styling in component CSS that themes should own

Indicators:
- Colors, shadows, border-radius, hover background in `.razor.css` targeting MudBlazor elements via `::deep`
- Hardcoded color values instead of `var(--mud-palette-*)` tokens
- Component CSS painting `.active`, `:hover`, `:focus` states on menu items, nav links, or list items
- Dark-mode overrides inside component CSS (`[data-theme*="dark"]` in `.razor.css`)

Fix: move visual rules to theme CSS under `Monica.UI/wwwroot/css/themes/*.css` using standard MudBlazor selectors (`.mud-menu-item`, `.mud-popover`, `.mud-card`, etc.). Keep only layout (sizing, positioning, flex, grid, overflow) in component CSS.

### P2 — Private component classes that force theme coupling

Indicators:
- Custom CSS classes on menu surfaces, row items, or overlay panels (e.g. `.my-dropdown`, `.custom-flyout-item`) that are also targeted in theme files
- Theme files containing selectors for these private component classes
- Grep theme files for the private class name to confirm coupling

Fix: remove private classes; use MudBlazor component parameters (`Class`, `PopoverClass`, `ListClass`) with standard or `mo-*` layout-only hooks. Layout hooks go in `mo-theme-main.css`.

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
5. **Clean up**: Delete any `.razor.css` file that becomes empty after removing visual rules. Remove unused `@using` directives.
6. **Build**: `dotnet build 'D:\Code\MoLibrary\Monica.UI\Monica.UI.csproj' -m` — must produce 0 warnings.
7. **Report**: List what was changed, which theme files may need a new shared selector, and any remaining items that need manual UI verification.

## Constraints

- Do NOT add new visual styling to component CSS — if something needs a per-theme look, it belongs in theme CSS on standard MudBlazor selectors.
- Do NOT break existing responsive or compact-mode behavior.
- Layout-only hooks (sizing, scroll, positioning) go in `mo-theme-main.css` with `mo-*` prefix, injected via `PopoverClass` / `ListClass` / `Class` parameters.
- Active route state: use `.active` class via `NavigationRouteMatcher.GetActiveClass()`, styled by themes on `.mud-menu-item.active`.
- Follow `mo-ui-development` SKILL.md Rules #9 (Theme-First Visual Simplicity and Component Responsibility) and #10 (Use MudBlazor Primitives for Interactive UI) as the authoritative reference.

## Component CSS Responsibility Reference

| Layer | Owns | Does NOT own |
|-------|------|----|
| MudTheme (C#) | Palette tokens, typography | Component-specific visuals |
| Theme CSS (`themes/*.css`) | Visual language on standard MudBlazor selectors | Layout, positioning |
| Shared layout CSS (`mo-theme-main.css`) | Layout hooks via `mo-*` classes | Visual styling |
| Component CSS (`.razor.css`) | Layout, sizing, positioning, responsive rules | Colors, shadows, hover effects |
