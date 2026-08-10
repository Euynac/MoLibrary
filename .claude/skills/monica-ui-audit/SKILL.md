---
name: monica-ui-audit
description: Audit and fix Monica Blazor UI compliance, including async lifetime, JS interop ownership, Precision visual hierarchy, subject-specific design direction, palette and typography roles, unnecessary effects or decoration, full-width operational layouts, prototype fidelity, offline fonts, theme tokens, MudBlazor primitives, responsive containment, CLR type formatting, and duplicate utilities. Use for UI audits, theme reviews, prototype comparisons, disposal failures, visual-quality regressions, clipping, wasted width, or broken responsive sizing.
---

# Blazor UI Compliance Audit

Audit Blazor components for runtime lifecycle safety and the Monica UI rules defined in `monica-ui-development`. Runtime safety takes precedence. For default-theme, global visual-language, or prototype-fidelity work, read `../monica-ui-development/references/monica-precision-design-language.md` before auditing.

## How to Determine Audit Target

1. If the user specifies a file/directory, or a stack trace identifies a component, state owner, or UI module, audit that target.
2. If the user says "all", scan production `.razor`, `.razor.cs`, UI `State/`, and UI `Support/` owners across the repository. Use resource-ownership searches such as `IJSObjectReference`, `DotNetObjectReference`, `IAsyncDisposable`, `IDisposable`, timers, event subscriptions, and fire-and-forget task assignments to find non-component owners.
3. If the user gives no target and no failure identifies one, scan `Monica.UI/Shell/Components/` and `Monica.UI/Shell/Pages/` by default.
4. For a single component, read its `.razor`, `.razor.cs`, and `.razor.css` files together. Also read any injected page-state/lifecycle owner, its DI registration, and every JavaScript module or callback target that the component owns.

## Violation Categories

Scan for these violations in order of severity:

### P0 — Unsafe async component lifetime or JS interop ownership

Indicators:
- `OnAfterRenderAsync`, an event callback, a navigation callback, timer, or background task can remain incomplete while `DisposeAsync` disposes an `IJSObjectReference`, `DotNetObjectReference`, cancellation source, or other resource that its continuation later uses
- A nullable `IJSObjectReference` field is treated as usable merely because it is non-null; disposal leaves the disposed reference published, or initialization can publish a newly imported reference after disposal
- An async continuation touches component state, requests a render, or invokes JavaScript after an `await` without a component-lifetime cancellation/disposal check when the awaited operation can outlive the component
- Cancellation can abandon a resource-producing operation, such as a JS module import, without observing and disposing a late-created result
- Fire-and-forget work is untracked, uncancelable, or allowed to report exceptions outside the renderer's observed lifecycle task
- A component-owned callback/render delegate, event subscription, timer, or cancellation source remains attached after disposal begins
- A disposable transient service is injected into a component at all, or a scoped disposable intended to match component lifetime is resolved from the longer-lived app/circuit scope; manual disposal adds a second conflicting owner
- Multiple render/event paths can initialize, invoke, replace, or dispose the same JS module without explicit single ownership, serialization, or idempotent teardown
- `DotNetObjectReference.Create(...)` is passed inline to JavaScript or otherwise not retained and deterministically disposed by its .NET owner; JavaScript retaining the reference does not transfer disposal ownership unless an explicit, verified ownership contract says so
- `DotNetObjectReference` is disposed before every JS observer, event listener, animation-frame callback, or other callback producer that can invoke it has been stopped
- Per-component JavaScript state is stored in module globals or located with document-global selectors, so overlapping old/new component instances can mutate or tear down each other's state; queued animation frames, timers, or observers survive the JS session's cleanup
- `DisposeAsync` invokes JavaScript to mutate or clean up DOM that the renderer may already have removed; DOM cleanup should be owned by client-side `MutationObserver` logic
- Server-side JS module calls or disposal ignore expected circuit loss (`JSDisconnectedException`), or code broadly swallows `ObjectDisposedException` instead of repairing the ownership race

Fix: give the component/state owner one explicit lifetime. Mark it disposed and cancel ordinary lifetime-bound work before teardown; stop scheduling work and detach callbacks/render delegates; recheck lifetime after incomplete awaits; prevent new resource acquisitions; serialize with or drain existing users; then atomically detach shared references for teardown. Always observe resource-producing operations and dispose late-created references instead of publishing them. Stop JS callback producers before disposing their `DotNetObjectReference`, and dispose every resource once. Do not register component-consumed disposable transients. Prefer a non-disposable factory that creates a component-owned resource. Use an explicit component-owned DI scope only after inspecting the full dependency graph and proving it does not require services bound to the existing Blazor circuit scope. Keep JS session state per component/root and cancel all queued work during session cleanup. Use client-side `MutationObserver` for DOM cleanup. Catch `JSDisconnectedException` where server-circuit loss is expected. Do not catch `ObjectDisposedException` from a locally owned interop reference; repair the ownership and teardown ordering.

### P1 — Custom interactive markup replacing MudBlazor primitives

Indicators:
- Custom dropdown/flyout/overlay markup with manual `@onmouseenter` / `@onmouseleave` / `@onclick` state machines
- Manual `_isOpen`, `_isHovered`, delayed-close `CancellationTokenSource` patterns
- Custom `<div class="dropdown-menu">` / `<div class="flyout-menu">` / `<div class="popover-panel">` instead of `MudMenu`, `MudPopover`, `MudDialog`, `MudDrawer`

Fix: replace with MudBlazor primitives. Reference implementation: `NavBarDropdown.razor`, `NavBarMore.razor` in `Monica.UI/Shell/Components/Layout/`.

### P2 — Broken layout containment, sizing, or scroll ownership

Indicators:
- An operational dashboard, workbench, administration page, or diagnostics page uses an arbitrary page-shell `width` or `max-width`, leaving usable shell width empty
- A `FullWidth` dialog, drawer, tab panel, or preview has a descendant `width`, `max-width`, grid track, or intrinsic `inline`/`fit-content` size that leaves usable space empty
- A flex/grid child that must shrink omits `min-width: 0`, or a grid uses `1fr` where `minmax(0, 1fr)` is required
- A component-isolated selector targets a MudBlazor render root it cannot reach; use a scoped native wrapper or a reachable `::deep` descendant selector
- Long hashes, identifiers, JSON, tables, or code stretch an ancestor instead of wrapping or scrolling inside the intended local surface
- The page or dialog becomes the horizontal scroll owner when only a table, diff, or code surface should scroll
- A fixed/minimum height leaves unexplained blank space instead of using content-driven height with a viewport-aware cap

Fix: trace the layout from the dialog/page surface to the failing descendant and assign width, shrink, wrap, and overflow responsibility explicitly. Make operational page roots consume the owner's full available width; apply readable line-length limits to local text regions instead of the page shell. Remove contradictory internal width caps, use `width: 100%`, `min-width: 0`, `minmax(0, 1fr)`, `overflow-wrap`, or a local scroll surface only where each property expresses the intended contract. Keep long identifiers fully accessible for inspection and copying. Justify every retained page-level width cap or fixed/minimum height with a concrete editorial, readability, interaction, or viewport requirement.

### P3 — Theme bypass, visual-language drift, or prototype-fidelity loss

Indicators:
- Inline `Style=` / `style=` attributes for layout or visuals
- Hardcoded color, shadow, radius, or background values instead of MudBlazor parameters, `var(--mud-palette-*)`, or approved `var(--mo-color-*)` tokens
- Semantically distinct statuses, severities, categories, or progress states collapse into visually identical neutral surfaces when scanability requires differentiation
- Multiple hierarchy levels and semantic states rely on the same neutral surface plus one accent, producing a flat or monotonous page
- The page has no subject-specific design thesis or palette/type/layout/signature plan, and its composition could be reused unchanged for an unrelated AI administration product
- Light or dark mode is excessively dark, low-contrast, or monotone, so canvas, structural surfaces, borders, text hierarchy, or semantic states collapse together
- Success, warning, error, information, selection, and runtime states are not meaningfully differentiated when the task requires rapid scanning
- Repeated facts, properties, metrics, or records are each wrapped in cards instead of rows, lists, definition groups, or tables
- More than one visual system competes as the page's signature treatment, or a gradient/glow/pattern is not backed by the accepted prototype or continuous-data semantics
- A page shell, app bar, navigation surface, ordinary card, KPI tile, panel, filter, table, data group, or status surface uses a decorative gradient
- Actionable controls, rows, or cards lack visible hover, focus, pressed, or selected feedback
- Informational surfaces translate, scale, change the cursor, or use strong lift on hover, falsely implying an unavailable action; subtle border, tonal, or low-shadow spatial-focus feedback is valid
- Motion uses large travel, bouncing, repeated flourishes, continuous ambient effects, or ignores `prefers-reduced-motion`
- A surface stacks more than two decorative cues, such as top stripe + ring + shadow, or repeats stripes/rings across every card
- The same accent rail is repeated across unrelated card groups, replaces a clearer page-specific outline/icon/badge treatment, or appears where it encodes no additional status or category meaning
- Border radii fall outside the 6/8/12 scale, multiply a base radius, or grow across nested surfaces
- The implementation loses the prototype's typography, density, spacing rhythm, composition, surface roles, width utilization, or responsive reflow
- Interface, hierarchy, and diagnostic text have no deliberate font-role separation, or monospace is used broadly as decoration
- A font declared by the prototype/source is missing from local WOFF2 assets, lacks `@font-face`, fails offline, has no explicit fallback stack/CJK coverage, or is silently replaced by a fallback
- Component CSS repainting global MudBlazor behavior that should be consistent across modules
- Component CSS painting route `.active`, `:hover`, or `:focus` states for menu items, nav links, or list items that are themed globally
- Dark-mode overrides inside component CSS (`[data-theme*="dark"]` in `.razor.css`)

Fix: articulate the subject-specific thesis and palette/type/layout/signature plan; rebuild hierarchy with typography and spacing; assign explicit surface and semantic-color roles; keep ordinary surfaces structurally simple; convert repeated data cards to rows/lists/tables; enforce the 6/8/12 radius scale; restore useful hover/focus/pressed/selected feedback; and simplify flamboyant, repetitive, or misleading effects. Keep one coherent prototype-backed signature system concentrated in identity/readiness, with only limited non-competing recurrence, and use gradients elsewhere only for continuous-data encoding. Use short purposeful motion, test `prefers-reduced-motion`, vendor role-appropriate fonts locally as WOFF2, and verify both primary and fallback paths. Compare prototype and browser at widths 1440, 929, and 390 in light and dark modes, then self-critique for generic AI-dashboard styling. Keep component-owned layout and token-based presentation in component CSS; move only shared MudBlazor behavior to theme CSS.

### P4 — Private component classes that force theme coupling

Indicators:
- Custom CSS classes on menu surfaces, row items, or overlay panels (e.g. `.my-dropdown`, `.custom-flyout-item`) that are also targeted in theme files
- Theme files containing selectors for these private component classes
- Grep theme files for the private class name to confirm coupling

Fix: remove theme coupling. If the class is only used by the owning component, keep it in the component's `.razor.css`. Promote a hook to shared CSS only when multiple components intentionally share the same layout contract.

### P5 — Duplicate utility or raw CLR type-display logic

Indicators:
- Text-resolution helpers like `GetItemText(NavigationItem)` duplicated across components instead of using `NavigationItem.ResolveDisplayText(localizer)`
- Manual route-matching wrappers instead of `NavigationRouteMatcher.GetActiveClass()`
- Repeated localization key lookup patterns that could use shared model methods
- Ordinary UI labels, tooltips, table cells, or drawer details receive type names from `Type.FullName`, `Type.AssemblyQualifiedName`, or `Type.ToString()`, exposing CLR generic backticks, `[[...]]` assembly-qualified arguments, version, culture, or public-key-token metadata
- Razor components format `System.Type` locally instead of receiving one normalized display value from the producing diagnostics, facade, view-model, or state boundary

Fix: use the shared utilities in `Monica.UI/Shell/Support/` and methods on `Monica.UI/Shell/Models/NavigationItem`. Format user-visible CLR type identities with `Monica.Tool.Extensions.GenericTypeExtensions.GetCleanFullName()` at the producing boundary, then pass the clean string to Razor. Do not expose assembly-qualified type identity in ordinary UI. Showing an assembly name or identity remains valid when assembly loading, binding, inventory, or module provenance is the explicit subject of the view.

## Audit Workflow

1. **Scan**: Read all `.razor`, `.razor.cs`, and `.razor.css` files in the target scope. Follow injected component-state owners, DI lifetimes, owned JS modules, JS-to-.NET callbacks, subscriptions, timers, and background tasks. Trace user-visible type-name strings back to their producer and search production UI paths for direct `Type.FullName`, `Type.AssemblyQualifiedName`, and `Type.ToString()` formatting.
2. **Trace lifetime**: Starting from initialization, render callbacks, UI/navigation events, and JS callbacks, mark every incomplete `await` and every fire-and-forget edge. Trace what can run after component disposal begins, who owns each disposable reference, which operation can publish or detach it, and whether teardown is idempotent. Do not infer safety from Blazor's synchronization context: component code is re-entrant at incomplete awaits and disposal timing relative to lifecycle tasks is nondeterministic.
3. **Trace containment**: For every dialog, drawer, grid, split view, table, or code/diff surface in scope, follow every ancestor from the outer surface to the content. Check width/max-width, flex/grid shrinkability, rendered selector reachability under CSS isolation, long-token behavior, height constraints, and which element owns each scrollbar. When selector reachability is suspect, verify the target's computed style or compare the compiled isolated selector with the rendered DOM; do not infer success from a class name alone.
4. **Detect**: For each violation, note the file path, line range, category (P0–P5), evidence, and the component that should own the fix.
5. **Report**: Present the report template below before making changes. Ask the user to confirm which violations to fix if the scope is large.
6. **Fix**: Apply concrete fixes, preserving all existing functionality, localization, and responsive behavior.
7. **Clean up**: Delete any `.razor.css` file only when the component no longer owns layout or localized presentation rules. Remove unused `@using` directives.
8. **Build**: Build the affected project with the WSL Windows-path rule, for example `dotnet build '<windows-project-path>' -m` for the specific UI module project. The build must produce 0 warnings.
9. **Verify lifetime deterministically**: When P0 applies, test the teardown interleaving rather than relying only on manual navigation. Block an initialization or JS invocation, begin disposal, then release the blocked continuation; assert that no disposed reference is invoked, a late-created reference is disposed instead of published, late continuations perform no state mutation, JS invocation, or render request beyond required cleanup of their own late-created resource, and repeated disposal is safe. In browser verification, rapidly navigate away/remount during first render and refresh work. For Blazor Server, also exercise circuit loss where feasible and assert no unhandled `ObjectDisposedException` or `JSDisconnectedException`.
10. **Verify layout and fidelity in browser**: Capture side-by-side prototype/browser evidence at viewport widths 1440, 929, and 390 in light and dark modes. Compare the thesis, signature treatment, palette and semantic roles, typography roles, density, spacing, surface roles, width utilization, radii, decoration count, interaction feedback, motion, and reflow. Exercise hover, keyboard focus, pressed, and selected states; emulate `prefers-reduced-motion` and confirm nonessential transforms and decorative animation stop without losing visible state meaning. Verify local WOFF2 loading plus the declared offline fallback path; confirm no runtime font/CDN dependency. For the document, dialog surface, and every element not intentionally designated as a local horizontal scroller, assert `scrollWidth <= clientWidth`. For every operational page root and surface intended to fill its owner, compare its rendered width with the owner's available content width. Record the intended local scroll owner and exercise short and long content.
11. **Verify type display**: Inspect rendered type labels and their tooltips. Outside explicit assembly-diagnostics views, confirm generic types contain no CLR backticks, `[[...]]` arguments, `Version=`, `Culture=`, or `PublicKeyToken=` metadata, and confirm the producing boundary uses `GetCleanFullName()` rather than a Razor-local formatter.
12. **Report**: List what changed, the design thesis and signature system with its placements, palette/semantic checks, font-role and offline-fallback evidence, lifetime interleavings tested, fidelity comparisons, browser assertions, type-display assertions, intentional local scrolling, any continuous-data gradient or page-width exceptions, and remaining manual verification.

## Report Template

| Priority | Location | Evidence | Correct owner/fix | Verification |
|---|---|---|---|---|
| P0–P5 | File and line | Rendered or source-level failure | Component/state/theme/layout owner | Build plus category-appropriate lifetime or browser verification |

When P0 is in scope, include the tested initialization/invocation/disposal ordering and rapid navigation/remount result. For visual work, include the thesis, signature system and placements, palette/semantic and font-role evidence, 1440/929/390 light/dark fidelity results, operational-page width utilization, `scrollWidth <= clientWidth`, intentional local horizontal scrollers, and every continuous-data gradient or retained page-width constraint.

## Constraints

- Do NOT add hardcoded visual styling to component CSS. Component isolation CSS may own component-specific layout, overflow, sizing, truncation, and localized presentation that uses MudBlazor theme tokens.
- Do NOT catch `ObjectDisposedException` from a locally owned interop reference. Repair ownership, cancellation, post-await guards, in-flight user coordination, callback shutdown order, and idempotent teardown.
- Do NOT dispose a `DotNetObjectReference` while JavaScript can still invoke it. Stop every callback producer before releasing the reference.
- Do NOT use JS interop from component disposal to perform DOM cleanup. Use client-side `MutationObserver` cleanup; reserve disposal for releasing owned interop references and non-DOM resources.
- Do NOT add gradients to operational shells, ordinary cards, KPI tiles, panels, filters, tables, repeated data, or status surfaces. Permit one focused prototype-backed signature system concentrated in identity/readiness, with limited non-competing recurrence, plus gradients that encode continuous data; keep supporting surfaces coherent and document the role.
- Do NOT constrain an operational page shell with an arbitrary `max-width`; constrain only the local editorial or text region that needs readable line length.
- Do NOT add decoration before hierarchy and surface roles are clear. Permit purposeful token-based effects only within the Precision decoration budget.
- Do NOT remove interaction feedback in the name of restraint. Prefer short, moderate, state-explaining transitions and always provide a reduced-motion path.
- Do NOT break existing responsive or compact-mode behavior.
- Do NOT treat `overflow-x: hidden` on the page or dialog as a containment fix; repair the child width contract and keep scrolling on the smallest surface that needs it.
- Do NOT render `Type.FullName`, `Type.AssemblyQualifiedName`, or `Type.ToString()` directly in ordinary UI. Use `GetCleanFullName()` at the producing boundary; reserve assembly identity for views explicitly about assemblies or binding provenance.
- Layout-only hooks (sizing, scroll, positioning, truncation) should usually stay in the component's `.razor.css`. Use `mo-*` shared CSS only for truly shared layout utilities used across multiple modules.
- Active route state: use `.active` class via `NavigationRouteMatcher.GetActiveClass()`, styled by themes on `.mud-menu-item.active`.
- Follow `monica-ui-development` Rule #1 (CSS Isolation), Rule #4 (Lifecycle and JS Interop), Rule #9 (Precision Visual Language and Component Responsibility), and Rule #10 (Use MudBlazor Primitives for Interactive UI).
- For lifecycle semantics, use the current Microsoft guidance on [Blazor synchronization context](https://learn.microsoft.com/aspnet/core/blazor/components/synchronization-context), [component disposal](https://learn.microsoft.com/aspnet/core/blazor/components/component-disposal), [component-owned DI scopes and their circuit-service limitations](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection#access-server-side-blazor-services-from-a-different-di-scope), and [JavaScript interop disposal and DOM cleanup](https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability) rather than assuming lifecycle/disposal ordering.

## Component CSS Responsibility Reference

| Layer | Owns | Does NOT own |
|-------|------|----|
| MudTheme (C#) | Palette tokens, typography | Component-specific visuals |
| Theme CSS (`themes/*.css`) | Visual language on standard MudBlazor selectors | Layout, positioning |
| Shared layout CSS (`mo-theme-main.css`) | Cross-module utilities and app-shell/global layout contracts | Component-specific layout or presentation |
| Component CSS (`.razor.css`) | Component-specific layout, sizing, positioning, responsive rules, truncation, localized token-based presentation | Hardcoded colors, theme variants, global MudBlazor behavior |
