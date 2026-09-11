# Monica Shell Redesign — UI Design

> Created: 2026-07-18  
> Last Updated: 2026-07-18  
> Status: Design prototype; not production implementation

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give developers and operators a calm, legible command center for navigating Monica's large, dynamically registered module surface without forcing categories into a crowded horizontal bar. |
| **Aesthetic Direction** | **Editorial command center / restrained luxury** — a quiet instrument panel with magazine-like hierarchy, warm paper or deep ink surfaces, exact rules, generous negative space, and dense data only where density earns its place. |
| **Typography** | **Playfair Display** for decisive editorial moments and **Source Sans 3 / Source Sans Pro** for interface copy and tabular data. Both families already have offline WOFF2 assets in `Monica.UI/wwwroot/fonts`, so production does not need a remote font dependency. |
| **Color Palette** | Light: parchment background, clean white working surfaces, ink text, moss status, and restrained brass accents. Dark: near-black green ink, lifted graphite surfaces, pale parchment text, and muted brass. The dark theme is composed independently rather than mechanically inverted. |
| **Signature Detail** | A slim **living edge** runs beside the active context and through selected rows. It behaves like a precise editorial rule and an operational pulse: static by default, gently breathing only while live data is refreshing. |
| **Constraints** | Dynamic registry-driven navigation; all existing search, notification, theme, language, and user actions remain available; desktop/tablet/mobile layouts; keyboard and focus support; reduced-motion support; production assets remain offline; production colors use MudBlazor or approved Monica theme tokens only. |

## Source Discovery

The prototype is grounded in the current Monica shell rather than inventing a separate product:

- `MainLayout.razor` currently provides a fixed top bar over one full-width scroll owner.
- `NavBar.razor` groups `IPageRegistry` items by localized category, measures available width, and sends excess categories into **More**.
- `NavBarActions.razor` exposes global search, notifications, theme selection, language selection, and the user menu.
- `NavBarBrand.razor` exposes app name, app id, version, and home navigation.
- `ThemeDialog.razor` supports multiple theme families plus independent light/dark mode.
- Registered categories in the repository include AI, Documentation, Knowledge & Retrieval, Monitor, Configuration, Debug, Module, Task Scheduling, and Infrastructure.
- The Module System dashboard itself has System Overview, Performance Monitoring, Module Management, Dependencies, and Assembly Analysis tabs.

The redesign preserves these capabilities but changes their spatial model. Categories stop being width-sensitive dropdowns and become a stable primary rail; pages in the current category become a readable contextual rail.

## Overview

The Monica shell becomes a three-layer workspace:

1. **Global rail** — brand, registered module categories, and durable shell state.
2. **Context rail** — pages inside the active category, application identity, current environment, and collapse control.
3. **Content stage** — a quiet utility header and a generous page canvas owned by the active Monica module.

The shell should feel composed even when only a few modules are registered, and remain usable when every Monica module is present. The prototype deliberately demonstrates the densest realistic registry state.

## Information Architecture

```text
Monica shell
├── Global rail
│   ├── Monica mark
│   ├── Registry categories
│   └── Compact user identity
├── Context rail
│   ├── Application identity + version
│   ├── Current category
│   ├── Registered pages
│   ├── Optional page-local saved views
│   └── Environment health + collapse
└── Content stage
    ├── Breadcrumb / environment
    ├── Search, notifications, language, theme, user
    ├── Page title and contextual actions
    └── Module-owned content
```

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| **Global rail** | Stable first-level navigation that cannot overflow horizontally. | Monica mark, category icon buttons, tooltips, active living edge, user avatar. |
| **Context rail** | Human-readable second level for the selected registry category. | App name/id/version, category title, page links, optional saved views, environment card, collapse button. |
| **Utility header** | Quiet global controls that stay reachable without dominating the page. | Breadcrumb, environment indicator, command search, notifications, language, theme, profile. |
| **Page masthead** | Gives every module a consistent arrival moment while leaving content ownership to the module. | Eyebrow, serif page title, description, page status, primary/secondary actions. |
| **Page tabs** | Hosts a module's own sub-navigation, such as the real Module System dashboard tabs. | Horizontally scrollable tab row, active rule, count/status annotations. |
| **Content canvas** | Demonstrates the shell around realistic Monica operational content. | Operational posture card, runtime metrics, module ledger, activity feed, dependency summary. |
| **Command palette** | Makes the full dynamic registry keyboard-addressable. | `Ctrl/Cmd + K`, live filter, grouped results, keyboard hints, page selection. |
| **Notification panel** | Shows operational events without navigating away. | Unread count, severity markers, timestamps, dismiss/view action. |
| **Mobile navigation sheet** | Recombines both rail layers for touch devices. | Category chips, active category page list, environment state, user actions. |
| **Feedback layer** | Confirms non-navigation actions without persistent chrome. | Toasts, refresh state, pressed/selected states. |

## Visual Hierarchy

### 1. Stable chrome

- The 76 px global rail uses the darkest surface in both modes and acts as a visual spine.
- The 272 px context rail is quieter and slightly lifted from the content background.
- The two rails are separate layers, not one oversized sidebar. This makes collapse meaningful and preserves icon-level navigation.

### 2. Arrival hierarchy

- Breadcrumb and environment state are small and functional.
- The page title is the only large serif expression in the working UI.
- Description width is capped so it reads like an editorial deck, not a banner.
- Actions align to the baseline of the masthead on desktop and move below it on narrow layouts.

### 3. Operational content

- One large posture panel leads the composition; metric cards support it rather than forming an undifferentiated card wall.
- Data tables use rules and spacing instead of a heavy container around every row.
- Status color is always paired with text or an icon.
- Brass is an identity/focus accent, not a semantic success color.

## Prototype Views and Data

The prototype seeds realistic registry data for all current Monica categories. Selecting a page changes the breadcrumb, title, description, status, masthead action, metrics, tab model, and supporting records. The default view is the real **Module System Dashboard**, with its actual five tab names and representative Monica module data.

The prototype is intentionally a shell study rather than a proposal to replace every existing module page. Its content canvas demonstrates:

- how a wide dashboard breathes inside the new shell;
- how tab-heavy pages fit without fighting global navigation;
- how dense tables, health states, and module names read in both themes;
- how the shell behaves when the registry contains many categories and pages.

## Key Interactions

### Navigate by rail

1. Select a category icon in the global rail.
2. The context rail changes category title and page list.
3. Select a registered page.
4. The active rule moves, the masthead and canvas update, and focus remains predictable.

On tablet, selecting a category opens the contextual rail as a temporary overlay. On mobile, the same model appears inside one navigation sheet.

### Collapse contextual navigation

1. Select the collapse button at the bottom of the context rail.
2. The context rail contracts to zero width; the global rail remains.
3. Category selection temporarily reopens the context layer so page navigation is never stranded.

The collapsed state should be persisted by the implementation as a user preference.

### Search the registry

1. Select Search or press `Ctrl/Cmd + K`.
2. Type a page or category name.
3. Results filter across the full registry, independent of the current category.
4. Select a result to navigate and close the palette.
5. `Escape` closes without navigation.

### Preview theme

1. Select the sun/moon control.
2. Light and dark surfaces, shadows, borders, and status treatments change as a coordinated theme.
3. The selected navigation and focus treatments retain sufficient contrast.

The production theme selector still owns Monica's full theme catalog; this binary control demonstrates the shell's theme-aware composition.

### Inspect notifications and account

- Notification and user controls open anchored panels.
- Only one transient panel is open at a time.
- Clicking outside or pressing `Escape` closes it.
- Language control cycles between English and Chinese in the prototype; production continues to use the configured supported culture list.

### Refresh operational data

- Refresh changes the live edge into a short pulse, rotates the refresh icon, and produces a compact confirmation toast.
- Repeated activation while refreshing is ignored.

## Motion and Animation

| Moment | Motion | Duration / Easing |
|--------|--------|-------------------|
| Initial arrival | Global rail, context rail, masthead, lead card, and support cards reveal in a short editorial sequence. | 320–620 ms, `cubic-bezier(.2,.8,.2,1)` with small stagger. |
| Category change | Context content cross-fades with a 6 px horizontal shift. | 180 ms. |
| Page change | Masthead and content fade/translate together; the shell chrome remains fixed. | 220 ms. |
| Context collapse | Rail width and content opacity coordinate; content stage expands smoothly. | 240 ms. |
| Command palette | Backdrop fades while the panel rises 10 px and settles. | 180 ms. |
| Refresh | Refresh glyph rotates once; living edge breathes twice. | 700 ms total. |
| Hover/focus | Border, ink, and 1–2 px translation only; no floating-card choreography. | 120–160 ms. |

`prefers-reduced-motion: reduce` disables reveals, transforms, smooth scrolling, the live pulse, and nonessential transitions. State changes remain immediate and legible.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| **Wide desktop (≥ 1440 px)** | 76 px global rail + 272 px context rail + full content stage. Masthead actions stay inline. Lead composition uses an asymmetric 7/5 grid. |
| **Desktop (1180–1439 px)** | Context rail narrows to 248 px. Page gutters reduce. Tables retain full structure. |
| **Tablet (768–1179 px)** | Global rail remains at 72 px. Context rail becomes an overlay opened from category selection or the header navigation button. Main dashboard becomes one column; metric cards use a two-column grid. |
| **Mobile (< 768 px)** | Both rails are replaced by a 60 px mobile header and a four-action bottom dock. Navigation opens as a full-height sheet combining category chips and contextual pages. Masthead actions stack, page tabs scroll horizontally, tables become labeled records, and utility actions remain thumb-reachable. |
| **Compact mobile (< 480 px)** | Gutter becomes 16 px; card padding reduces; secondary masthead action becomes icon-first; typography clamps; noncritical table columns stay hidden. |

## Accessibility Contract

- Every icon-only control has an accessible name and a visible tooltip on hover-capable devices.
- Global categories are a labeled navigation region; selected category and page expose `aria-current` or pressed state.
- Dialogs use `role="dialog"`, a labelled heading, focus entry, focus return, outside-click close, and `Escape` close.
- Focus rings use a two-layer treatment that remains visible against both light and dark rails.
- Minimum interactive target is 44 × 44 px on touch layouts.
- Status never depends on color alone.
- The page supports 200% zoom without horizontal page scrolling; only explicit tab/table regions may scroll.
- Decorative grain and contour lines are hidden from assistive technology and never intercept input.

## Theme and Token Strategy

The prototype defines exploratory CSS custom properties with literal colors so it can run independently. Production must map the visual roles to Monica's theme contract; it must not copy prototype hex values into Razor, CSS, JavaScript, or C# payloads.

| Visual role | Production token direction |
|-------------|----------------------------|
| Page background | `--mud-palette-background` |
| Raised and navigation surfaces | `--mud-palette-surface`, `--mud-palette-drawer-background` |
| Primary/secondary ink | `--mud-palette-text-primary`, `--mud-palette-text-secondary` |
| Rules and dividers | `--mud-palette-divider`, `--mud-palette-lines-default` |
| Selected navigation | `--mud-palette-primary`, plus palette-derived hover/selected states |
| Success/warning/error/info | Matching `--mud-palette-*` semantic roles |
| Supplemental shell accent | Existing approved `--mo-color-*` only when no Mud palette role expresses the intent |
| Shadows and overlays | Existing Mud elevation/overlay tokens or theme-derived RGB channels |

Production font loading uses the repository's offline `PlayfairDisplay-*.woff2` and `SourceSansPro-*.woff2` assets. Production icons use MudBlazor's bundled icon path; the prototype's Lucide CDN is design-only.

## Production Handoff Contract

The prototype defines the shell's spatial and interaction contract, not production component code. The production pass should preserve the following boundaries:

### Shell ownership

- The shell owns only global category navigation, contextual page navigation, global utilities, responsive overlays, and the page arrival frame.
- A registered page owns everything below the masthead and page-level tabs. The shell must not make assumptions about a module's tables, charts, dialogs, or internal routes.
- `IPageRegistry` remains the single source for category and page navigation. Labels, ordering, icons, localization keys, and routes must not be duplicated in layout-specific lists.
- The selected category is derived from the active registered page/route so deep links arrive with both rail layers synchronized.

### Layout and scroll ownership

- Desktop uses one viewport-height shell: fixed global rail, fixed contextual rail, fixed utility header, and exactly one content-stage vertical scroll owner.
- Tablet uses the same content scroll owner while the contextual rail moves into a modal overlay with a scrim. Opening it must not alter the content width or scroll position.
- Mobile removes both desktop rails from layout and adds top/bottom safe-area-aware chrome. The navigation sheet owns its internal overflow while open; background content is inert and does not scroll.
- Module pages may own explicitly bounded horizontal scrolling for tabs, tables, terminals, graphs, or code. The document itself must not develop horizontal overflow at 200% zoom.

### MudBlazor and Monica UI constraints

- Compose production controls from MudBlazor primitives where they express the interaction correctly; keep custom CSS focused on shell composition and the editorial identity.
- Do not copy prototype literal colors, shadows, gradients, or opacity mixes into production UI. Resolve every production color through `--mud-palette-*` first and the approved `--mo-color-*` supplement only when no Mud role is expressive enough.
- Keep `Monica.UI/wwwroot/css/mo-theme-main.css` color-token-only. Shell component styling belongs with the component or its dedicated stylesheet, not in the shared color contract.
- Use the repository's offline font assets and MudBlazor icon pipeline. Google Fonts, Tailwind CDN, and Lucide are prototype-only and must not become production runtime dependencies.
- Theme behavior must continue to respect the full registered Monica theme catalog and independent light/dark selection. The prototype's binary switch is only a fast composition preview.

### State, localization, and accessibility

- Persist only durable preferences: contextual-rail collapse, selected theme/mode, and culture. Overlay visibility, command queries, notification panels, and page-local tabs are transient.
- Render every user-facing shell label through the established localization system. Category/page labels continue to resolve from registry localization keys; production must not ship the prototype's English fixtures as hardcoded text.
- Desktop rail tooltips are supplementary; no action may depend on hover. Category/page selection, command search, collapse, overlays, and sheets must all work by keyboard, pointer, and touch.
- Dialog/sheet opening moves focus inside, traps focus while modal, closes on `Escape` and scrim activation, makes the background inert, and returns focus to the invoker. Tablet and mobile overlays announce their heading and selected navigation state.
- `prefers-reduced-motion` removes reveal, translate, pulse, and smooth-scroll effects. It must not delay state updates or hide progress/status feedback.

### Responsive state transitions

- Crossing breakpoints must reconcile open state: desktop closes tablet/mobile overlays; tablet closes the mobile sheet; mobile ignores the stored desktop collapse preference without deleting it.
- The persistent desktop rail does not shrink to an unusable intermediate sidebar. At tablet width it becomes an overlay; below 768 px both layers become the combined navigation sheet.
- Touch targets remain at least 44 × 44 px, bottom-dock controls account for safe-area insets, and the navigation sheet remains usable with the on-screen keyboard visible.

## Prototype Acceptance Matrix

| Scenario | Expected result |
|----------|-----------------|
| Desktop category navigation | Global rail remains persistent; selecting a category updates the context rail and opens a representative page without horizontal overflow. |
| Desktop context collapse | Context rail collapses cleanly, content expands, and selecting a category restores access to page navigation. |
| Tablet navigation | Global rail remains visible; category or header activation opens the contextual rail above an inert-looking scrim; selection closes the overlay. |
| Mobile navigation | Desktop rails are absent; the fixed header and four-action utility dock remain visible; Navigate opens a combined category/page bottom sheet. |
| Pointer and touch | Every navigation and utility action responds to a normal click/tap with no hover-only dependency. |
| Command search | `Ctrl/Cmd + K` opens registry search; typing filters; arrow keys move the active result; `Enter` navigates; `Escape` closes. |
| Theme preview | Theme action switches between independently composed parchment and ink themes on desktop, tablet, and mobile. |
| Reduced motion | With reduced motion enabled, navigation and state feedback remain immediate while reveals, transforms, smooth scrolling, and pulses are suppressed. |
| Narrow content | Masthead actions stack, tabs scroll within their own region, metrics remain readable, and ledger rows become labeled records. |
| Production handoff | No prototype CDN, fixture, or literal palette value is treated as a production dependency or token source. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Global navigation model | Persistent category rail + contextual page rail | Removes horizontal overflow and hover-only flyouts while matching the registry's existing category/page model. |
| Navigation density | Compact icons globally, readable labels contextually | Supports many modules without hiding what the current category contains. |
| Header role | Utility strip inside content stage | Keeps global actions visible but allows the page title to lead. |
| App identity | Context rail header | App name, id, and version remain durable without consuming the full top width. |
| Page width | Generous responsive canvas, not a narrow centered document | Monica contains dashboards, graphs, terminals, tables, and chat; the shell must not impose one restrictive max width. |
| Card language | Few structured surfaces with internal rules | Avoids the generic dashboard pattern of identical floating cards. |
| Dark mode | Purpose-built ink palette | Maintains hierarchy and restrained depth instead of simply inverting parchment. |
| Responsive navigation | Rail → overlay rail → combined mobile sheet | Keeps one information architecture across device classes rather than creating unrelated menus. |
| Persistence candidates | Theme, culture, collapsed context rail | These are durable preferences; transient panels and page-local tabs are not shell preferences. |
| Backward compatibility | Not a design constraint | The branch permits a cleaner shell architecture and breaking layout changes. |

## File Map

```text
.ui-design/monica-shell-redesign/
├── design.md    # This specification
├── index.html   # Semantic prototype shell
├── styles.css   # Responsive themes, composition, motion, accessibility
├── data.js      # Realistic registry and page-view fixtures
└── app.js       # Navigation, palette, panels, theme, locale, refresh behavior
```

The prototype is static and browser-runnable with no build step. It uses Tailwind and Lucide through CDNs only where allowed for design exploration.
