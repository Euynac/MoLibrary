# Markdown Docs Redesign — Latest UI Direction

> Created: 2026-03-11
> Last Updated: 2026-03-11

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Redesign the Monica Markdown page into a documentation workspace for internal users reading manuals, runbooks, technical notes, and structured knowledge base content. |
| **Aesthetic Direction** | Editorial + utilitarian. The reader feels like a refined technical journal, while the navigation and outline rails behave like precise tools rather than generic admin sidebars. |
| **Typography** | `Cormorant Garamond` for page and section emphasis, `Barlow` for controls and navigation, and `IBM Plex Mono` for metadata, status text, and compact technical accents. |
| **Color Palette** | Warm paper neutrals, deep ink text, cobalt as the navigational accent, and restrained brass highlights for structure and metadata. |
| **Signature Detail** | A Monica-owned outline rail with a quiet neutral guide track and a single cobalt active segment. The right side should feel like a margin index, not a second navigation tree. |
| **Constraints** | Monica-owned TOC only. Do not rely on the built-in MudBlazor/MoMarkdown TOC drawer. Keep the center search affordance as a placeholder for now. Rails should stay fixed within the viewport and manage their own overflow. |

## Overview

The latest design direction is a three-region documentation workspace:

- a left navigation rail for group switching and document tree browsing
- a dominant center reader card for toolbar, breadcrumbs, and article content
- a separate right outline rail for the Monica-owned TOC

This replaces the older concept where the TOC lived inside the reader card. The current direction is more explicit, closer to the prototype, and better matches the requirement that navigation and outline each feel like independent rails.

## Goals

- Keep the left tree view aligned with Monica’s current visual language.
- Make the center reading surface feel deliberate, readable, and calmer than a standard admin page.
- Show breadcrumb navigation, a visible search placeholder, and a TOC visibility toggle in the reader toolbar.
- Use a separate right-side outline rail driven by Monica heading data.
- Keep the navigation rail and outline rail fixed within the viewport with their own scrollbars.
- Narrow the outline rail so it reads as a compact margin index rather than a full-width panel.
- Avoid the built-in MudBlazor/MoMarkdown TOC UI because it is constrained to the markdown content region and cannot satisfy the target layout.

## Recommended Layout

### Desktop Structure

```text
+--------------------------------------------------------------------------------------+
| Navigation Rail | Reader Card                                      | Outline Rail   |
|                 | +----------------------------------------------+ |                |
|                 | | Search Placeholder | TOC Toggle | Meta       | | Outline title  |
|                 | +----------------------------------------------+ | TOC list        |
|                 | | Breadcrumb row                               | | own scrollbar   |
|                 | +----------------------------------------------+ |                |
|                 | |                                              | |                |
|                 | | scrollable article                           | |                |
|                 | |                                              | |                |
|                 | +----------------------------------------------+ |                |
+--------------------------------------------------------------------------------------+
```

### Layout Behavior

- The overall page shell owns the viewport height.
- The page should not depend on body scroll for primary reading behavior on desktop.
- The left navigation rail keeps its own internal scroll region for the tree.
- The center article keeps its own scroll region.
- The right outline rail keeps its own internal scroll region.
- The outline rail is intentionally narrower than the navigation rail.

### Why This Structure

- It matches the current prototype direction better than the older “TOC inside reader” idea.
- It satisfies the requested three functional areas without flattening them into one large composite panel.
- It makes the outline rail easier to hide or narrow without disturbing the reader layout.
- It keeps viewport height ownership at page level and local scrolling inside the actual working regions.

## Rail Behavior

### Navigation Rail

- Fixed-width card on desktop, about `300px` to `340px`.
- Contains:
- knowledge base switcher
- tree filter/search input
- nested tree list
- collapse or hide affordance
- The tree list is the primary scroll region inside the card.
- The card should remain at full available viewport height under the page shell.

### Reader Card

- Dominant center surface.
- Contains:
- top toolbar
- breadcrumb row
- article scroll region
- The article content scrolls independently from the left and right rails.
- The article width remains centered and readable instead of expanding edge to edge.

### Outline Rail

- Separate Monica-owned card on desktop.
- Width should feel compact, around `220px` to `240px`.
- Internal left gutter should be tight rather than heavily padded.
- The TOC list owns its own scrollbar and remains bounded by viewport height.
- Active-state styling uses a neutral rail with a cobalt segment and a subtle active text emphasis.

## Component Design

### 1. `UIMarkdownPage`

Responsibilities:

- own page-level loading state
- own selected group/document/tree state
- own current hash anchor
- own sidebar visibility state
- own outline visibility state
- own current heading list for the outline rail

Recommended state:

```csharp
private bool _showSidebar = true;
private bool _showTableOfContents = true;
private IReadOnlyList<MoMarkdownHeading> _documentHeadings = [];
```

Recommended behavior:

- keep document and location loading logic in the page
- keep existing hash observer logic
- receive heading updates from the content viewer
- pass headings and current anchor into the outline rail
- reset `_documentHeadings` when the selected document changes
- navigate by hash through `MarkdownViewerLocation`

### 2. `DocumentSidebar`

Responsibilities:

- knowledge base switcher
- document tree
- collapse action

Visual direction:

- convert the old sidebar shell into a full navigation rail card
- keep the existing tree filtering control
- keep the tree area as the scrollable region
- avoid the old border-right-only feel

### 3. `DocumentContentViewer`

Responsibilities:

- render the top toolbar
- render breadcrumbs
- show last-edited metadata
- show search placeholder only
- render markdown content
- continue heading parsing through `MoMarkdown`
- report headings upward to the page

Important change:

- built-in markdown TOC rendering should be disabled completely

Recommended `MoMarkdown` shape:

```razor
<MoMarkdown Value="@_strippedContent"
            ScopeKey="@Document.GroupKey"
            DocumentRelativePath="@Document.RelativePath"
            HasTableOfContents="false"
            HeadingsChanged="HandleHeadingsChanged" />
```

Toolbar content:

- left: sidebar toggle if needed plus breadcrumbs
- center/right: visible search placeholder
- far right: outline toggle and last-edited text

Search direction:

- visual placeholder only
- no real filtering logic in this phase
- no fake search state in the production component design

### 4. `DocumentTableOfContentsPanel`

Recommended new component:

- `Monica.Markdown/UIMarkdown/Components/DocumentTableOfContentsPanel.razor`
- `Monica.Markdown/UIMarkdown/Components/DocumentTableOfContentsPanel.razor.css`

Responsibilities:

- render outline heading list from `IReadOnlyList<MoMarkdownHeading>`
- highlight the active heading using `CurrentAnchorId`
- navigate to `#heading-id` when an item is clicked
- visually indent by heading level
- remain compact and lightweight
- show an empty state when there is no document or no headings

Recommended API:

```razor
<DocumentTableOfContentsPanel Headings="_documentHeadings"
                              CurrentAnchorId="_currentAnchorId"
                              IsVisible="_showTableOfContents"
                              OnHeadingSelected="OnHeadingSelected" />
```

Implementation model:

- use Monica heading data already produced by `MoMudMarkdown`
- mimic MudBlazor Markdown TOC behavior without depending on internal types
- keep heading levels `<= 3`
- use local scroll handling in the outline rail
- allow a flattened list with level-based indentation instead of a nested tree

Recommended click behavior:

- page owns navigation
- TOC panel raises selected heading id
- page navigates to `new MarkdownViewerLocation(_selectedGroupKey, _selectedDocument?.RelativePath, headingId)`
- existing hash observer updates active state after navigation

Not recommended:

- using `MudTableOfContents`, `MudTableOfContentsNavMenu`, or other MudBlazor.Markdown internal components directly

## Interaction Model

### Document Selection

- Selecting a document in the tree updates:
- article content
- breadcrumbs
- metadata
- outline rail content
- active heading state

### Outline Toggle

- Toolbar button toggles `_showTableOfContents`.
- When hidden, the reader card reclaims the width.
- When visible, the separate right outline rail appears.

### Active Heading

- Existing hash observation continues to drive `_currentAnchorId`.
- The outline rail highlights the current heading.
- The active outline item should remain visible within the TOC scrollbar as the article scrolls.

### Sidebar Visibility

- Desktop: sidebar can be hidden and reopened from a docked affordance.
- Smaller screens: sidebar becomes an overlay panel.

### Empty States

- No document selected: show a centered reader empty state.
- No headings available: show a muted outline empty state instead of hiding the rail unexpectedly.

## Responsive Behavior

### Desktop

- Three visible regions in practice:
- navigation rail
- reader card
- separate outline rail
- Breadcrumbs sit below the primary toolbar row.
- Navigation and outline remain height-bounded and scroll independently.

### Tablet

- Reader remains dominant.
- Sidebar can collapse to free width.
- Outline can hide by default when space is constrained and return via toolbar toggle.
- If stacked, the outline should become a shorter bounded panel rather than an endlessly tall block.

### Mobile

- Sidebar becomes an overlay sheet reopened from a dock button.
- Reader becomes the default full-width view.
- Outline is hidden by default or shown as a compact secondary panel.
- Search overlay uses simplified spacing and occupies most of the viewport.

## Styling Direction

- Preserve Monica’s neutral surface palette and outlined card language.
- Avoid flat border-right shell styling.
- Keep toolbar compact and documentation-oriented rather than dashboard-heavy.
- Keep the content column readable with a centered article width.
- Make the outline rail visually lighter than the reader.
- Reduce excess margin and padding inside the outline rail.
- Use motion sparingly:
- staggered page reveal
- calm overlay transitions
- smooth active-outline movement

## File Map

### Prototype Files

| File | Purpose |
|------|---------|
| `.ui-design/markdown-docs-redesign/index.html` | Prototype layout skeleton |
| `.ui-design/markdown-docs-redesign/styles.css` | Visual system, rail sizing, viewport behavior, responsive layout |
| `.ui-design/markdown-docs-redesign/data.js` | Mock groups, tree nodes, and document content |
| `.ui-design/markdown-docs-redesign/app.js` | Tree filtering, document switching, outline behavior, and interaction logic |

### Expected Implementation Files

- `Monica.Markdown/Pages/UIMarkdownPage.razor`
- `Monica.Markdown/Pages/UIMarkdownPage.razor.css`
- `Monica.Markdown/UIMarkdown/Components/DocumentContentViewer.razor`
- `Monica.Markdown/UIMarkdown/Components/DocumentContentViewer.razor.css`
- `Monica.Markdown/UIMarkdown/Components/DocumentSidebar.razor`
- `Monica.Markdown/UIMarkdown/Components/DocumentSidebar.razor.css`
- `Monica.Markdown/UIMarkdown/Components/DocumentTreePanel.razor.css`
- `Monica.Markdown/UIMarkdown/Components/DocumentTableOfContentsPanel.razor`
- `Monica.Markdown/UIMarkdown/Components/DocumentTableOfContentsPanel.razor.css`
- `Monica.Markdown/Localization/MarkdownResource/en-US.json`
- `Monica.Markdown/Localization/MarkdownResource/zh-CN.json`

## Localization Additions

Likely new resource keys:

- `MarkdownViewer:Toolbar:SearchPlaceholder`
- `MarkdownViewer:Toolbar:ShowOutline`
- `MarkdownViewer:Toolbar:HideOutline`
- `MarkdownViewer:States:NoOutlineAvailable`

## Implementation Sequence

1. Refactor `UIMarkdownPage` to own heading list and outline visibility.
2. Convert the page shell into navigation rail + reader card + optional outline rail.
3. Refactor `DocumentContentViewer` to remove built-in TOC rendering and expose toolbar controls.
4. Add `DocumentTableOfContentsPanel`.
5. Adjust sidebar and tree styling to match the new card-based rail layout.
6. Add localization keys.
7. Build `Monica.Markdown` and verify layout and scrolling behavior in the app.

## Review Points

Please confirm these before implementation:

1. Preferred structure: separate navigation rail, reader card, and dedicated outline rail on desktop.
2. Monica-owned TOC only; built-in MudBlazor/MoMarkdown TOC remains disabled.
3. Search remains a placeholder-only affordance in this phase.
4. Navigation rail and outline rail should stay height-bounded with their own scrollbars.
