# System Info Redesign — UI Design

> Created: 2026-08-07
> Last Updated: 2026-08-07

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give developers and operators a trustworthy, quickly scannable profile of the running service: identity, readiness, runtime, environment, endpoints, and artifact evidence. |
| **Aesthetic Direction** | **Operational dossier / control-room profile.** Editorial hierarchy and controlled technical density replace the previous collection of unrelated cards. It should feel like opening a precise service record, not a generic admin dashboard. |
| **Typography** | **Oxanium** for identity, duration, and evidence numbers; **IBM Plex Sans + Noto Sans SC** for interface copy. The contrast creates a recognizable instrumentation character while preserving bilingual readability. |
| **Color Palette** | Warm fog canvas and ink surfaces, with Monica ultraviolet as the identity signal, cyan for live runtime evidence, and emerald only for factual ready/healthy states. Dark mode uses deep blue-black surfaces rather than simple inversion. |
| **Signature Detail** | A readiness pulse rail connects the opt-in start marker to host readiness. Its moving signal freezes into an exact duration, making startup timing spatially legible without turning the page into a chart dashboard. |
| **Constraints** | Read-only information except refresh/restart actions; optional startup timing must disappear completely when untracked; light/dark and zh-CN/en-US; no document-level overflow; production implementation must remain possible with MudBlazor and Monica theme tokens. |

## Overview

The page is a single service dossier for engineers diagnosing a live Monica host. The first viewport answers four questions in order:

1. Which service and instance am I viewing?
2. Is it ready, and how long has it been running?
3. If startup tracking was opted into, when did the host become ready and how long did it take?
4. Where do I inspect runtime, environment, network, and build evidence?

The prototype deliberately avoids equal-weight cards. Identity and readiness are the hero, compact pulse facts are the second layer, and detailed evidence is grouped below into operational sections.

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Service identity hero | Strong shell/product identity with only available version and process facts. | Monogram tile, identity lockup, environment badge, product version, process start, uptime. |
| Readiness pulse rail | Opt-in startup timing visual that exists only when a marker was supplied. | Start/ready nodes, elapsed rail, exact duration, ready timestamp, tracked badge. |
| Pulse strip | Four current operational facts with consistent anatomy. | Live-updating uptime, captured working set, process ID, capture age. |
| Dossier index | Sticky local navigation between evidence groups. | Section buttons, active marker, compact viewport controls. |
| Runtime evidence | Process/runtime facts arranged for fast comparison. | Definition rows, exact working-set instrument, architecture and privilege badges. |
| Environment fingerprint | Host, user, OS, paths and clock context. | Identity rows, long-value containment, disclosure panel. |
| Endpoint registry | Listening addresses as operational resources, without inferring role, reachability, or certificate state. | Derived scheme badge, exact address, inspect action. |
| Artifact evidence | Product and file evidence without pretending file metadata is compiler provenance. | Product/file versions, artifact file timestamp, company and legal metadata. |
| Configured links | Preserves arbitrary `CustomLinks`, including category, target and optional credential actions. | Category groups, target marker, optional access disclosure, text-labelled credential copy actions. |
| Restart confirmation | Clearly separates refresh from destructive host shutdown request. | Modal, supervisor warning, cancel/confirm actions. |

## File Map

```text
.ui-design/system-info-redesign/
├── design.md     # Direction, hierarchy, behavior, and handoff notes
├── index.html    # Semantic prototype shell
├── styles.css    # Visual system, layout, motion, responsive states
├── data.js       # Bilingual copy and realistic service evidence
└── app.js        # Theme, language, tracking state, navigation, modal, refresh
```

## Interactions

- **Tracked / untracked switch:** changes the mock host contract. Tracked shows the readiness rail and exact timing. Untracked removes every startup-timing fragment and lets service identity occupy the full hero width.
- **Theme and locale:** switch instantly and persist only for the current prototype session. All interface labels and generated evidence states rerender.
- **Dossier index:** scrolls to the chosen evidence group and follows the current group via `IntersectionObserver`.
- **Refresh:** briefly marks the page as capturing, updates the captured timestamp/age, animates the live dot, and raises a transient confirmation toast.
- **Restart service:** exists only when `EnableSelfRestartAction` is true. Its confirmation includes the configured `SelfRestartDelay`, explains graceful shutdown/external-supervisor ownership, and visually isolates the destructive request from routine refresh.
- **Endpoint actions:** open a detail sheet containing only the exact address, address-derived scheme and capture timestamp. It does not infer listener role, TLS certificate state, or network reachability.
- **Configured links:** render the host-provided list without assuming fixed Monica routes. Category, link target, enabled state and optional credentials remain data-driven. Credentials stay hidden until the explicit access disclosure is opened; only these credential values get text-labelled copy actions.
- **Density control:** switches evidence groups between the full dossier and a compact operational scan without hiding identity or readiness.

## Motion and Animation

- Page entry uses one staged reveal: identity, readiness rail, pulse strip, then evidence groups.
- The readiness rail has a restrained travelling pulse and breathing ready node. It stops under `prefers-reduced-motion`.
- Refresh retriggers a short signal sweep and capture-age update instead of replacing the whole page with a loading state.
- Hover lifts only actionable rows; factual evidence rows remain visually stable.
- Dialog and toast use small opacity/translation transitions and never block reduced-motion users.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (≥ 1200 px) | Asymmetric hero (identity 5/12, timing 7/12), sticky left dossier index, two-column evidence canvas, wide endpoint registry. |
| Tablet (768–1199 px) | Hero timing occupies its own full row, pulse facts form a 2×2 grid, dossier index becomes a sticky horizontal section rail, evidence remains two columns where content allows. |
| Mobile (< 768 px) | Header actions become icon-first, hero stacks, the dossier index becomes a labeled section picker, all evidence is one column, endpoint actions remain contained, and only the pulse strip may scroll horizontally. No document overflow. |

## Tracked and Untracked States

### Tracked

- Readiness rail is visible.
- `Tracked / 已记录` is factual, not a health score.
- Exact application startup duration and ready timestamp are shown once.
- Process uptime remains separate from startup timing.

### Untracked

- The entire readiness-timing panel is absent: no placeholder, zero, em dash, disabled rail, or setup prompt.
- The service identity hero expands and keeps the factual `Ready` state plus process uptime.
- All other runtime and environment diagnostics remain available.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Page model | One service dossier rather than equal cards | System metadata has an information hierarchy; flattening it makes important evidence harder to find. |
| Startup presentation | Conditional readiness rail in the hero | The timing is important when present, but optional by contract. The rail shows only start-marker and ready endpoints backed by the timing contract. |
| Status color | Emerald only for current ready state | Warning/error colors are reserved for genuine conditions; environment and categories use neutral/violet/cyan signals. |
| Long paths | Dedicated contained evidence rows with ellipsis and full-title tooltip | Prevents overflow without littering the page with copy icons. |
| Refresh state | Preserve evidence while recapturing | Avoids a disruptive blank/loading page for a lightweight read-only refresh. |
| Restart placement | Secondary header action plus explicit confirmation | Keeps the operation discoverable but clearly separate from normal inspection. |
| Mobile navigation | Labeled section picker | Avoids another clipped tab carousel while retaining direct access to every evidence group. |

## Operational States

| State | Presentation and behavior |
|-------|---------------------------|
| Initial load | Preserve the page frame and service title while skeleton versions of hero, KPI strip, and first evidence rows load. Do not replace the full document with a centered spinner. |
| Fatal initial failure | Replace the evidence canvas with a compact error surface containing a localized reason, retry action, and no stale operational claims. The page header remains available. |
| Refresh in progress | Keep the last successful dossier visible, disable duplicate refresh, animate only the refresh signal, and update capture time after success. |
| Refresh failure | Keep the last successful evidence in place, retain its capture age, and show a localized non-destructive error notification with retry. |
| Startup untracked | Remove the complete readiness-timing panel and all timing badges. Identity expands; no placeholder or setup prompt is rendered. |
| Missing environment or file data | Omit only unavailable rows/sections and render a localized, contained empty state when an entire evidence domain is unavailable. File evidence must not depend on environment data. |
| No endpoints | Replace the registry with a localized empty state; never label the host as listening when the address collection is empty. |
| No configured links | Omit the configured-links section and its dossier-index entry. With many links, preserve categories and page/collapse locally without extending a single unbounded list. |
| Restart disabled | Omit the restart action completely. |
| Restart requested | Disable the action after acceptance, label it as requested, preserve the configured-delay message, and reject duplicate requests. |

## Data Fidelity Boundary

- Existing System Info data supports product/file identity, process time and uptime, absolute working set, process ID, bitness, privilege flag, machine/user/OS/path/time-zone facts, listening address strings, and optional startup duration/ready time.
- `Is64BitProcess` is displayed as `64-bit process`, never inferred as `x64`.
- `BuildTime` is sourced from the file last-write timestamp and is therefore labeled `Artifact timestamp`, not authoritative compilation time.
- A listening address yields only the exact address and parsed scheme. Listener priority, certificate provenance, and network reachability are not inferred.
- Working set is a captured absolute value. No arbitrary denominator, utilization percentage, memory budget, or trend is fabricated.
- Environment-variable collections remain outside the default page because they may contain secrets and create unbounded visual noise.
- The factual `Ready` label means this interactive page is being served after host startup; it is not a health-check result or composite score.

## Production Handoff Notes

- Map all prototype colors to MudBlazor palette roles or approved `--mo-color-*` tokens; the hexadecimal palette exists only for visual exploration.
- Keep the page shell thin and preserve current service/API ownership. The prototype is an information-architecture target, not a backend contract proposal.
- The app bar in `index.html` is ambient preview chrome for visual context. Production already owns this shell and must implement only the System Info page content beneath it.
- Preserve native semantic headings and definition-list relationships. Visual rails and gauges need accessible text equivalents.
- The readiness rail must not render when `ApplicationStartupDurationMs` or `ApplicationReadyAtUtc` is absent. It uses only the available start-marker and ready facts; no invented intermediate stages are shown.
- Service-instance IDs, region labels, health scores, endpoint role, TLS provenance, and reachability are deliberately absent because the current response contract does not supply them.
- Preserve arbitrary configured custom-link categories, targets, credentials and enabled states rather than implementing the three mock examples as fixed routes.
- Render restart only when enabled and interpolate the actual configured delay. The modal must trap focus, close on Escape, and restore focus to its trigger.
- Localize visible copy, tooltips, modal language, empty states, and ARIA labels in the dedicated System Info resource.
- Avoid introducing raw assembly-qualified names, global JavaScript objects, fixed DOM IDs, or ubiquitous copy buttons during implementation.

## Prototype Verification

- JavaScript syntax validation passes for `data.js` and `app.js`.
- English and Chinese dictionaries contain the same 105 keys; every statically referenced prototype key resolves.
- Browser-tested 24 combinations: 1440×1000, 929×678, and 390×844; English/Chinese; light/dark; tracked/untracked.
- Every combination has `document.scrollWidth === document.clientWidth`. The mobile KPI strip is the only intentional local horizontal scroller.
- Desktop/tablet use the section rail; mobile replaces it with the labeled section picker.
- Tracked timing appears only when enabled. Untracked mode contains no visible startup title, duration, rail, badge, or placeholder.
- Refresh preserves existing evidence, prevents duplicate requests, and resets capture age after completion.
- Endpoint inspection, configured-link credential disclosure, restart delay/requested state, Escape close, focus trap, and focus restoration were exercised successfully.
- Reduced-motion emulation reduces all animation and transition durations to effectively zero.
- Browser execution produced no JavaScript or resource errors. The prototype-only Tailwind CDN emits its standard development-use warning; production will use Monica/MudBlazor assets and theme tokens instead.
