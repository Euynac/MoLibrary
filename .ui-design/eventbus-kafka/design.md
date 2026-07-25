# EventBus Kafka — UI Design

> Created: 2026-07-26
> Last Updated: 2026-07-26

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give platform operators a fast, trustworthy view of Kafka-backed EventBus integration health, cluster context, inventory, consumer lag, and live throughput. |
| **Aesthetic Direction** | Industrial observability console: restrained, information-dense, and operational rather than decorative. |
| **Typography** | Barlow Condensed for compact operational labels and Manrope for readable body copy in the prototype. Production keeps Monica's theme-owned typography. |
| **Color Palette** | Theme-owned neutral surfaces with primary violet for focus, success for reachable/live signals, info for inventory, warning for limited access, and error only for actionable failures. |
| **Signature Detail** | A compact “signal board” pairs a dominant integration/cluster context panel with six icon-led metrics whose accent rails communicate category and state. |
| **Constraints** | Preserve Monica routing, Facade/State behavior, MudBlazor 9 primitives, localization, keyboard navigation, dark mode, CSS isolation, and local-scroll ownership. |

## Overview

The console is used by platform engineers who need to answer three questions quickly: which EventBus/Kafka mode is active, which cluster is currently in context, and whether inventory or throughput needs attention. The redesign keeps the existing five work areas while making the dashboard a useful operational summary instead of a flat collection of equally weighted cards.

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Control header | Identifies the console, exposes refresh/create actions, and summarizes the active cluster and access state. | Hero surface, Kafka glyph, status chips, primary/secondary actions |
| Signal board | Separates integration context from operational metrics and prevents long provider names from colliding. | Context panel, responsive metric grid, semantic icon badges |
| Cluster context | Keeps the selected cluster, bootstrap/access mode, and refresh affordance visible as a compact footer strip. | Status indicator, wrapped identifier, refresh action |
| Workspaces | Preserves Dashboard, Clusters, Topics, Consumer Groups, and Performance tabs with a consistent heading/action rhythm. | MudTabs equivalent, section header, local-scroll tables |
| System states | Makes loading, empty, error, warning, live, and disabled states distinct without changing business semantics. | Skeletons, alerts, empty-state panel, disabled controls |

## Interactions

- Refresh keeps the current workspace and cluster context, disables competing actions, and surfaces completion through the existing snackbar behavior.
- New cluster remains the primary page-level action and opens the existing dialog.
- Tabs preserve their current business actions; only visual hierarchy and responsive containment change.
- The prototype's state selector demonstrates normal, loading, empty, and error presentation without implying a production-only control.
- Theme toggle demonstrates that hierarchy is carried by semantic tokens rather than light-only colors.

## Motion and Animation

- One short staggered reveal introduces the context panel and metric tiles on first load.
- Hover and focus use small elevation/outline changes only; no motion is required to understand state.
- Tab changes use a restrained opacity/translate transition.
- `prefers-reduced-motion` disables all non-essential movement.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (lg+) | Header content and actions share a row. Context panel occupies roughly one third; metrics form a three-column grid. |
| Tablet (md) | Header remains compact; context spans the row and metrics form two or three columns depending on available width. |
| Mobile (sm) | Header actions wrap at full width; context is full width; metrics remain a compact two-column grid; tab strip and data tables own their local horizontal scrolling. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Dashboard composition | One context panel plus six compact metrics | Fixes the existing 6+1 orphan row and separates identity from measurements. |
| Metric color | Semantic accent rail/icon badge, neutral readable body | Adds scanability while remaining compatible with every Monica theme. |
| Provider wrapping | Full value, multi-line wrapping, no string truncation | Operational identifiers stay inspectable and cannot overlap adjacent cards. |
| Mobile density | Two metric columns with shorter supporting copy | Preserves overview value without seven full-width cards consuming the entire panel. |
| Overflow | Tables/dialog payloads remain the smallest local scroll owners | Prevents page-level horizontal scrolling and keeps actions reachable. |

