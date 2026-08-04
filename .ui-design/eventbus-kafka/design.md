# EventBus Kafka - UI Design

> Created: 2026-07-26
> Last Updated: 2026-08-03

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give platform operators a fast, trustworthy view of Kafka-backed EventBus integration health, cluster context, inventory, consumer lag, and live throughput. |
| **Aesthetic Direction** | Restrained industrial operations console: precise, information-dense, and operational rather than decorative. |
| **Typography** | Barlow Condensed for compact operational labels and Manrope for readable body copy in the prototype. Production keeps Monica's theme-owned typography. |
| **Color Palette** | Theme-owned neutral surfaces with primary violet for focus, success for reachable/live signals, info for inventory, warning for limited access, and error only for actionable failures. |
| **Signature Detail** | A numbered three-layer rhythm repeats from page to dialog: identity context first, runtime summary second, evidence and next actions last. |
| **Constraints** | Preserve Monica routing, Facade/State behavior, MudBlazor 9 primitives, localization, keyboard navigation, dark mode, CSS isolation, and local-scroll ownership. |

## Overview

The console is used by platform engineers who need to answer three questions quickly: which EventBus/Kafka mode is active, which cluster is currently in context, and whether inventory or throughput needs attention. This refinement keeps the existing five work areas and the staged visual language, but makes the reading order explicit: identity context, runtime summary, then evidence and next actions. Short workspaces remain content-driven while long inventories scroll inside the smallest data-owning surface.

## Audit-led refinement

The 2026-08-03 source and browser audit tightened the original direction in four places:

- Global refresh and new-cluster ownership stays in the page header. Workspace headers expose only actions unique to that workspace, such as topic creation, filtering, capture, or live-sampling controls.
- Status is never communicated by color alone. Reachability, limited access, sampling, empty, and error states pair semantic color with localized text and an icon.
- Dashboard evidence links to safe next steps instead of introducing a second refresh control or a new destructive shortcut.
- Long tables and large diagnostics use one vertical body scroller plus the smallest necessary horizontal evidence scroller; actions and dialog identity remain reachable at 390 x 844.

## Hierarchy Blueprint

| Layer | Operator question | Page treatment | Dialog treatment |
|-------|-------------------|----------------|------------------|
| **01 · Identity context** | What provider, cluster, topic, or consumer am I operating on? | Provider and selected-cluster surfaces sit together, keep full identifiers visible, and carry reachability/access status. | Title, group/topic identity, capture freshness, and close affordance remain visible before controls. |
| **02 · Runtime summary** | Is the system healthy, busy, limited, or unknown? | Six compact metrics form a stable signal rail with semantic accents and tabular values. | Four summary metrics precede members and partition evidence. |
| **03 · Evidence / next actions** | What proves the state, and where should I go next? | Access evidence and safe workspace navigation share a bounded lower layer. Destructive actions remain with the row or object they affect. | Tables own local horizontal scrolling; controls and primary close/save actions remain reachable. |

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Control header | Identifies the console and exposes the existing refresh/create actions. | Hero surface, Kafka glyph, primary/secondary actions |
| Signal board | Separates integration context from operational metrics and prevents long provider names from colliding. | Context panel, responsive metric grid, semantic icon badges |
| Cluster context | Keeps the selected cluster and bootstrap/access mode visible without duplicating global actions. | Status text, semantic indicator, wrapped identifier |
| Evidence and next actions | Uses the lower dashboard area for real access evidence and safe navigation instead of blank viewport fill. | Evidence rows, capability status, inventory/performance navigation |
| Workspaces | Preserves Dashboard, Clusters, Topics, Consumer Groups, and Performance with a consistent heading/action rhythm. | Tabs, workspace header, local-scroll tables |
| Cluster editor | Converts the existing cluster fields from one flat grid into scannable configuration sections without changing what is saved. | Identity section, connection ownership, security section, integration section, sticky actions |
| Consumer diagnostics | Leads with group/topic identity and capture status, then summarizes members, assignments, lag, and consume rate before detailed evidence. | Status banner, metric rail, members table, partition table |
| System states | Makes loading, empty, error, warning, live, and disabled states distinct without changing business semantics. | Progress, alerts, empty-state panel, disabled controls |

## Interactions

- The page-level Refresh is the single owner of the existing refresh behavior. It keeps the current workspace and cluster context, disables competing actions, and surfaces completion through the existing snackbar behavior.
- New cluster remains the primary page-level action and opens the existing dialog. The dialog groups the same fields into Identity, Connection ownership, Security, and Integration sections; no new save semantics are introduced.
- Tabs preserve their existing actions and data behavior.
- Long topic, cluster, bootstrap, and consumer identifiers remain fully available through wrapping or a local table scroller. The first line stays visually dominant while metadata and copy affordances remain adjacent.
- Routine topic actions are grouped into Inspect and Configure sections. Clear messages and Delete topic sit in a separately labeled danger group after a divider, while retaining the existing confirmation flows and remaining attached to the topic row they affect.
- Consumer detail opens from the existing members/details actions. Refresh and live polling retain their behavior while the controls form one 40px capture rail centered against the Group ID identity block.
- The dialog title stays concise. The complete Group ID and optional topic remain in the identity surface, so long operational identifiers are visible once instead of consuming both the title and body.
- Short workspaces use content-driven height. Long inventories cap against the viewport and keep vertical and horizontal scrolling on the smallest data-owning surface; pager controls remain outside the horizontal scroller and reachable on mobile.
- On mobile, dialogs become near-full-height sheets with fixed identity/header and action/footer regions around one vertical content scroller. Evidence tables remain independent horizontal scroll owners.
- The prototype demonstrates the consumer metrics drill-down, sectioned cluster editor, long-topic behavior, danger grouping, and light/dark presentation without adding production-only behavior.

## Motion and Animation

- One short staggered reveal introduces context and metric tiles on first load.
- Hover and focus use small elevation or outline changes only; no motion is required to understand state.
- Dialog entry uses one restrained opacity/translate transition.
- `prefers-reduced-motion` disables all non-essential movement.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (lg+) | Header content and actions share a row. Paired provider/cluster identity surfaces lead into a six-item summary rail and a two-column evidence/operations layer. Consumer detail centers its Group ID block and 40px capture rail on one horizontal axis above a four-item summary rail. |
| Tablet (md) | Identity surfaces and evidence/operations stack while metrics use two or three columns. Consumer identity and capture controls remain on one centered row while space permits, cluster form sections use two columns where useful, and summary metrics use two columns. |
| Mobile (sm) | Header actions use a full-width action grid; metrics remain a compact two-column rail; tabs and data tables own local horizontal scrolling. Dialogs become near-full-height sheets, capture controls stack in one readable column, and sticky actions surround one vertical content scroller. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Dashboard composition | Paired identity context, six compact metrics, then evidence/next actions | Makes the three operator questions visible as an intentional top-to-bottom sequence. |
| Metric color | Semantic accent rail/icon badge, neutral readable body | Adds scanability while remaining compatible with every Monica theme. |
| Provider wrapping | Full value, multi-line wrapping, no string truncation | Operational identifiers stay inspectable and cannot overlap adjacent cards. |
| Long topic identity | Full name wraps within the identity cell; the table remains the local horizontal scroll owner | Operators can inspect and copy the exact topic without stretching the page or losing numeric column alignment. |
| Dangerous actions | Separate labeled group after a divider inside the owning row menu, never promoted to the dashboard | Reduces accidental activation while preserving the existing confirmation behavior and object context. |
| Action ownership | Global refresh/new cluster in the page header; workspace-only controls inside each tab | Removes repeated controls, improves mobile density, and makes primary versus contextual actions unambiguous. |
| State communication | Icon, localized text, and semantic token together | Keeps reachability and warning states understandable without relying on color perception. |
| Cluster form hierarchy | Four semantic sections with short helper copy and a sticky action row | The same configuration fields become easier to scan, especially on narrow screens, without changing the data contract. |
| Mobile density | Two metric columns with shorter supporting copy | Preserves overview value without turning every metric into a full-width block. |
| Overflow | Tables and dialog evidence remain the smallest local scroll owners | Prevents page-level horizontal scrolling and keeps actions reachable. |
| Vertical sizing | Content-driven workspace plus a useful lower evidence/operations layer | Removes unexplained blank panel height while using desktop space for real evidence; long tables still receive a viewport-aware cap. |
| Drill-down hierarchy | Context banner, four summary metrics, members, then partitions | Operators can assess health before scanning identifiers and offsets, while all original evidence remains available. |
| Capture rail alignment | Shared 40px control height and one centered desktop/tablet axis; single-column mobile fallback | Removes mixed Switch/Input/Button baselines without squeezing labels at 390px. |
| Dialog title | Short task title with full Group ID owned by the identity surface | Avoids duplicate long identifiers and preserves more mobile space for diagnostic evidence. |
| Dialog scroll contract | One vertical body scroller; identity and actions stay reachable; each wide table scrolls locally | Prevents nested page scrolling and protects close/save access on 390px-wide screens. |
