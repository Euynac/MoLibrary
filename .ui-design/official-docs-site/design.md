# Official Docs Site — UI Design

> Created: 2026-04-21
> Last Updated: 2026-04-21

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Create a polished official website and documentation prototype for Monica, serving framework evaluation, quick onboarding, module exploration, and deep documentation reading. Primary users are .NET infrastructure developers, Monica maintainers, and teams evaluating Monica as a modular application foundation. |
| **Aesthetic Direction** | Refined technical editorial mixed with an infrastructure control-room. The page feels like documentation, product site, architecture map, and runtime console in one coherent surface. |
| **Typography** | `Bricolage Grotesque` for confident product headings, `Newsreader` for editorial documentation rhythm, and `JetBrains Mono` for module names, paths, and code. |
| **Color Palette** | Monica purple anchors identity, but the site avoids a generic purple-on-white look by pairing deep ink, warm porcelain, oxide amber, cyan telemetry, and graphite panels. |
| **Signature Detail** | A "module atlas" and command-palette docs reader that make the framework feel like a living modular system rather than a static docs directory. |
| **Constraints** | Design-only static prototype. No Blazor, C#, MudBlazor, build tools, or production implementation code. Prototype must run directly from `index.html` and remain self-contained under `.ui-design/official-docs-site/`. |

## Overview

This prototype combines Monica's official marketing page and docs reader into one cohesive experience. The landing page communicates Monica's value: independent modules, unified `Mo.Add*()` registration, dashboards, distributed-first infrastructure, and a real documentation product backed by the `Monica.Docs` modular-monolith API. The documentation surface emphasizes fast reading, module discovery, route-like navigation, and a search-first workflow.

## File Map

| File | Role |
|------|------|
| `index.html` | Static entry point and semantic page structure. |
| `styles.css` | Visual system, responsive layout, animations, docs shell, module atlas, search overlay, and theme mode. |
| `data.js` | Mock content based on current Monica and `../Monica.Docs` docs structure. |
| `app.js` | Prototype interactions: docs switching, search palette, module filtering, architecture stage switching, mobile drawer, copy CTA, and active section highlighting. |

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Global Header | Persistent product navigation and quick access to docs search. | Logo mark, nav links, language badge, signal mode toggle, command button. |
| Hero | Communicates Monica's position as modular .NET infrastructure. | Editorial headline, product CTAs, install command, module registration spine, status metrics. |
| Product Narrative | Explains why Monica is different from a monolithic framework. | Feature cards, module pattern diagram, API/content pipeline summary. |
| Docs Reader | Shows the future documentation experience for `../Monica.Docs`. | Tree navigation, article card, table of contents, reading-mode chips, mobile sidebar. |
| Module Atlas | Explorable module directory based on current zh-CN docs modules. | Category filter, module cards, package/registration/UI labels. |
| Architecture Flow | Explains how markdown source flows through the Documentation domain into frontend contracts. | Clickable pipeline stages, detail panel, contract preview. |
| Search Palette | Command-style search across docs and modules. | `Ctrl+K` open, filtered results, keyboard-friendly close/select behavior. |
| Footer | Reinforces current development status and docs repository role. | Status notes, architecture guardrails, project links. |

## Interactions

The header navigation scrolls to major sections and highlights the active section while scrolling. The search button and `Ctrl+K` open a command palette that searches docs and modules; selecting a docs result opens it in the reader. The docs tree switches article content and table-of-contents links. The module atlas filters by category. The architecture flow changes the explanation panel when a stage is selected. The signal mode toggle shifts the palette into a darker diagnostic console feel without changing layout.

## Motion and Animation

The page uses a staged reveal on load for hero elements and section cards. Module cards lift and expose diagnostic metadata on hover. The docs reader crossfades article content when navigation changes. The architecture flow uses a moving focus ring and soft glow. The search overlay fades and scales into place. Reduced-motion users receive a static version with animations disabled.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (lg+) | Asymmetric two-column hero, sticky docs sidebar and TOC, full module atlas grid, horizontal architecture pipeline. |
| Tablet (md) | Hero stacks into balanced sections, docs reader uses two-column content with condensed TOC, module cards become two columns. |
| Mobile (sm) | Header navigation collapses visually, docs tree opens as a drawer, module atlas becomes single column, command palette uses full-screen bottom sheet spacing. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Brand treatment | Keep Monica purple but embed it in an ink/porcelain/telemetry palette. | Preserves the existing logo identity while avoiding an overused AI-purple docs aesthetic. |
| Docs IA | Use current `docs/zh-CN` structure as the source: Getting Started, Concepts, Modules, Scenarios. | Matches the actual docs project and makes the prototype useful for the next implementation phase. |
| Primary interaction | Search-first docs reader with module atlas. | Monica has many independent modules, so exploration and lookup are more important than a linear marketing funnel. |
| Layout tone | Editorial density with control-room diagnostics. | Monica is infrastructure software; the UI should feel precise, inspectable, and technically serious. |
| Prototype split | Multi-file static prototype. | Keeps the prototype maintainable because the data and interaction logic are substantial. |
