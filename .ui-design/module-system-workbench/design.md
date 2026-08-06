# Module Diagnostics Workbench — UI Design

> Created: 2026-08-06
> Last Updated: 2026-08-06

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give Monica maintainers and application operators one trustworthy place to explain module composition: what happened, what blocked startup, where time was spent, what was discovered, and how the module graph is connected. |
| **Aesthetic Direction** | Precision observatory: an editorial/industrial console with controlled density, strong alignment, restrained surfaces, and evidence-first hierarchy. It should feel like a flight recorder, not a generic KPI dashboard. |
| **Typography** | Archivo for compact operational headings and Source Sans 3 for high-legibility body copy. IBM Plex Mono is reserved for timings, identifiers, and paths. Production should use Monica's corresponding local font assets. |
| **Color Palette** | Monica ink and cool neutral surfaces form the base. Cyan marks selection and live trace relationships, amber marks budget pressure, vermilion marks failure, and green is reserved for verified success. Both light and dark themes use the same semantic hierarchy. |
| **Signature Detail** | A selectable critical-path ribbon links time, module, and dependency evidence. Selecting a segment focuses the matching timeline row, module drawer, and dependency neighborhood. |
| **Constraints** | Static design prototype only; no Blazor, C#, or production code. The future implementation uses MudBlazor primitives and approved Monica theme tokens, supports keyboard/reduced-motion operation, and never exposes raw option objects. |

## Overview

The workbench replaces the long, card-heavy module dashboard with five deep-linkable sections. It serves framework engineers diagnosing startup behavior and application teams validating their own module composition. The default view answers four questions without scrolling: did composition finish, how long did it take, what dominated the critical path, and is there evidence requiring attention?

The prototype uses realistic FlightService data from FIPS2022: 93 active modules, 47 scanned assemblies, a 3,468 ms composition, a 2,577 ms service-registration window, and explicit type-discovery stages instead of two ambiguous `DiscoverTypes` rows.

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Global Command Bar | Identifies the host and snapshot while keeping comparison and refresh actions available. | Host identity, final-state badge, revision, theme/language controls, import/export, refresh. |
| Section Navigator | Provides stable, deep-linkable movement between five evidence views. | Sticky desktop rail, labeled mobile select, section status markers. |
| Overview | Gives a concise composition verdict and directs attention to evidence. | Five KPI cells, structured findings, hotspots, critical-path ribbon, host facts. |
| Performance | Explains wall-clock time, blocking causality, callback contributors, and startup work. | Waterfall, critical chain, top-10 controls, contributor table, discovery stages. |
| Modules | Supports rapid module inventory and selected-module inspection. | Faceted search, 25/50/100 paging, dense table, desktop side drawer/mobile sheet. |
| Dependencies | Explores direct topology without presenting an unreadable full graph by default. | One-hop neighborhood, focus selector, progressive expansion, direct-edge table alternative. |
| Discovery | Separates plan declaration, assembly resolution, type enumeration, query evaluation, and registration commit. | Stage strip, query contributions, scan outcome summary, lazy searchable assembly inventory. |
| Baseline Comparison | Adds portable regression context without server-side history. | Sanitized export, schema-checked import, demo baseline, KPI/timeline/module/edge deltas. |
| Help Drawer | Keeps explanations out of the main evidence plane. | Keyboard-accessible glossary, metric semantics, privacy notes. |

## Interactions

1. **Open the workbench**: Overview loads first. The snapshot identity and `Final · Succeeded` state establish that every section describes one immutable revision.
2. **Follow the critical path**: Selecting a ribbon segment highlights it, updates the evidence inspector, and offers direct routes to Performance, Modules, or Dependencies. The module drawer opens for module-owned segments.
3. **Inspect performance**: Top contributors default to 10 rows, a 1 ms threshold, and hidden zero-duration callbacks. Controls can expose the complete list without changing captured data.
4. **Inspect a module**: Search or facet the catalog, then select a row. The detail drawer presents summary, performance, direct dependencies, explicitly safe option diagnostics, and errors in separate tabs.
5. **Explore dependencies**: The largest blocking contributor is selected initially. One-hop dependencies and dependents are shown as a readable neighborhood; expanding the graph is explicit. A direct-edge table provides a keyboard/screen-reader alternative.
6. **Explain type discovery**: The Discovery view shows five non-overlapping stages. Assembly inventory is lazy, collapsed, failure-first, searchable, and paged.
7. **Compare a baseline**: Import accepts a sanitized snapshot with the matching schema version. A compatible baseline adds deltas and overlays; an incompatible file is rejected without mutating current state. The prototype includes a one-click demo baseline.
8. **Export safely**: Export downloads a mock sanitized payload containing no option values, assembly paths, stack traces, or raw exceptions.
9. **Use on mobile**: Section navigation becomes a labeled selector; the catalog becomes compact cards and the module drawer becomes a full-screen sheet.

## Motion and Animation

- Initial load reveals the command bar, KPI strip, and evidence panels in a short staggered sequence.
- Critical-path selection uses one coordinated cyan trace that moves across the ribbon and related evidence.
- Section changes cross-fade by 6 px rather than sliding whole screens.
- The module drawer enters from the inline end on desktop and rises as a full-height sheet on mobile.
- Graph nodes move only when neighborhood depth changes; selection uses outline and glow rather than scale.
- `prefers-reduced-motion: reduce` disables reveals, drawer travel, and graph transitions while preserving all state changes.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (`lg+`) | Sticky horizontal section rail, five-cell KPI strip, asymmetric evidence grids, full table catalog, inline-end module drawer, and graph plus evidence rail. |
| Tablet (`md`) | KPI strip wraps to 3+2, evidence grids stack selectively, drawer uses 70% width, and dependency evidence moves beneath the graph. |
| Mobile (`sm`) | Labeled section select replaces the tab rail, KPIs become a horizontally scrollable local strip, tables become cards or intentional local scrollers, and module details become a full-screen sheet. No document-level horizontal overflow. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Navigation | Five stable sections rather than one long dashboard. | Operators retain orientation, pages deep-link cleanly, and expensive details can load only when needed. |
| Overall status | Structural outcome plus explicit findings/budgets; no composite score. | A score hid semantics and made healthy-looking numbers indefensible. |
| Performance focal point | Critical-path ribbon synchronized across views. | Wall-clock causality is more useful than isolated aggregate durations. |
| Type discovery | Five factual stages with counts. | Removes the false impression that compilation runs twice and makes regressions attributable. |
| Module details | Drawer/sheet, not nested expansion rows. | Preserves catalog context while giving option and dependency evidence enough space. |
| Dependency default | Selected one-hop neighborhood. | A 93-node graph is visual noise; progressive expansion keeps topology legible. |
| Assembly inventory | Lazy, collapsed, failure-first, paged. | The full list is diagnostic detail and must not dominate the main page or initial payload. |
| Comparison | Portable sanitized import/export only. | Provides regression evidence without creating server history, retention, or access-policy concerns. |
| Theme | Light/dark semantic parity with restrained accent use. | Preserves Monica familiarity and keeps anomaly colors meaningful. |

## Prototype File Map

| File | Purpose |
|------|---------|
| `index.html` | Static entry point, semantic page shell, section containers, drawers, dialogs, and templates. |
| `styles.css` | Precision-observatory visual system, responsive layouts, graph/timeline treatment, themes, and motion. |
| `data.js` | Realistic FIPS2022 snapshot, modules, dependencies, discovery inventory, findings, and comparison baseline. |
| `app.js` | Navigation, synchronized selection, filters, drawers, graph mode, inventory paging, theme/language, and comparison import/export. |
