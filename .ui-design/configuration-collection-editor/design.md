# Configuration Collection Editor — UI Design

> Created: 2026-06-01
> Last Updated: 2026-06-01

## Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Give operators a safe visual editor for Dictionary and keyed List configuration nodes, without forcing whole-document JSON editing for common add/edit/remove flows. |
| **Aesthetic Direction** | Industrial/utilitarian: dense, restrained, path-aware, and optimized for repeated operational work. |
| **Typography** | Oswald for compact headings and Barlow for readable operational text. The pair feels technical without becoming decorative. |
| **Color Palette** | Ink black, graphite surfaces, muted steel borders, electric cyan for selected path, amber for restart impact, red for destructive staged changes, and green for valid additions. |
| **Signature Detail** | A persistent mutation path rail that turns visual selection into exact logical paths such as `Services[$billing].ConnectedDbs[#main].ConnectionString`. |
| **Constraints** | Prototype only. Static browser-runnable artifact using Tailwind CDN, Lucide icons, CSS, and small JavaScript state. Future MudBlazor implementation should keep Mud components and theme tokens, but this artifact is not implementation code. |

## Overview

The collection editor is a modal-scale operator console for complex configuration nodes. It replaces the current “raw JSON first” flow with a structured editor for two high-value shapes:

- `Dictionary<string, ServicesOptions>`: create, rename, clone, remove, and edit dictionary entries by key.
- `List<ConnectedDbOptions>` with a stable item key: create, edit, remove, and reorder list items without using index-based mutation paths.

Raw JSON remains available as an advanced tab for complete subtree replacement or emergency correction. The design assumes the mutation model can stage granular `Set`, `Remove`, and `Replace` operations by logical path.

## Modules

| Module | Description | Key Components |
|--------|-------------|----------------|
| Dialog Header | Identifies the definition, selected node, reload behavior, schema version, and dirty state. | Title, chips, restart warning, staged count. |
| Collection Mode Tabs | Switches between Dictionary, Keyed List, Raw JSON, and Patch Preview. | Segmented tabs, path rail, mode-specific actions. |
| Collection Navigator | Lists dictionary entries or list items with status, validation, and staged markers. | Search, add button, compact item rows, reorder controls for lists. |
| Item Editor | Edits selected entry fields using form controls derived from schema shape. | Text fields, switches, numeric inputs, nested list summary, validation badges. |
| Nested Collection Strip | Shows child collections inside an item, especially `ConnectedDbs`. | Horizontal keyed item chips, add/remove/edit affordances. |
| Diff/Patch Preview | Makes the exact staged mutations inspectable before staging into the state page. | Operation cards, logical paths, old/new JSON snippets. |
| Footer Action Bar | Controls staging, canceling, and fallback to replace mode. | Cancel, reset, stage changes. |

## Interactions

1. **Open dictionary node**: The operator clicks “Edit complex value” on `Services`. The editor starts in Dictionary mode, selects the first service, and shows exact path `Services[$billing]`.
2. **Edit dictionary entry**: Selecting an entry updates the center form. Changing a scalar field adds a staged `Set` operation against a leaf path.
3. **Add dictionary entry**: Add opens an inline key panel. The key is validated before fields become editable. Saving creates a staged `Set` for `Services[$newKey]`.
4. **Remove dictionary entry**: Remove marks the entry as pending deletion and adds a staged `Remove` for `Services[$key]`.
5. **Edit keyed list**: Switching to Connected DBs focuses `Services[$billing].ConnectedDbs`. Selecting `#main` edits the item by stable key instead of list index.
6. **Reorder list**: Reorder controls show a staged order change only when order is semantically useful. Item identity remains keyed.
7. **Raw JSON fallback**: Raw JSON tab exposes full subtree replacement. It is framed as advanced because it creates a broader `Replace` operation.
8. **Patch preview**: Preview tab summarizes every staged operation, its logical path, value kind, restart impact, and old/new JSON.

## Motion and Animation

- Dialog enters with a quick vertical reveal and subtle opacity fade.
- Mode switches slide the active panel horizontally by a few pixels.
- Selected path rail pulses once when the selected item changes.
- Staged changes animate in with a narrow left border and count update.
- Reorder controls use transform transitions only; reduced-motion users get instant state changes.

## Responsive Behavior

| Breakpoint | Layout Change |
|------------|---------------|
| Desktop (lg+) | Three-column layout: navigator, editor, patch rail. Dialog fills most viewport height with internal scroll panels. |
| Tablet (md) | Navigator becomes a top horizontal list. Editor and patch preview stack vertically. |
| Mobile (sm) | Single-column flow with mode tabs sticky at top. Patch preview collapses behind a summary button. |

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Collection UI shape | One Collection Studio dialog instead of separate Dictionary/List dialogs. | Dictionary and keyed list share selection, key validation, patch preview, and staging behavior. |
| Mutation visibility | Always show exact logical path. | The path is the contract operators are modifying; hiding it makes complex edits feel unsafe. |
| Default edit granularity | Leaf `Set` for scalar edits, item `Set` for new keyed entries, item/root `Remove` for deletion, explicit `Replace` for raw JSON. | Preserves small mutation history while allowing broad replacement when intended. |
| List identity | Stable item key is primary; index is shown only as display order. | Avoids index-based mutation fragility after reordering. |
| Raw JSON | Available but advanced. | Keeps power-user escape hatch without making it the normal workflow. |
| Reorder | Present only for lists and represented separately from identity. | Clarifies that list item key and visual order are different concepts. |

## Prototype File Map

| File | Purpose |
|------|---------|
| `index.html` | Static entry point and page shell. |
| `styles.css` | Industrial visual system, responsive layout, animations. |
| `data.js` | Mock schema and realistic configuration data. |
| `app.js` | Prototype state, tab switching, selection, staging, add/remove/reorder interactions. |
