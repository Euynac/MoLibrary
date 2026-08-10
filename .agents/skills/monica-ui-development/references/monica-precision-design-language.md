# Monica Precision Default Design Language

Use this contract for the default theme, global visual-language changes, and prototype handoff. Treat a supplied prototype as an implementation contract for composition, density, typography, spacing, and surface roles—not as loose inspiration.

## Build the hierarchy first

1. Establish information priority with type scale, weight, line height, whitespace, alignment, and density.
2. Name each surface role before styling it: canvas, section, ordinary surface, interactive/raised surface, semantic surface, or visualization.
3. Use tonal contrast and spacing for grouping. Add decoration only after the hierarchy reads correctly without it.
4. Keep ordinary surfaces flat. Reserve elevation for overlays, menus, dialogs, or content that genuinely sits above another layer.

## Use the available width

- Let operational dashboards, workbenches, administration pages, and diagnostics pages consume the full width provided by their shell. Use `width: 100%` and `min-width: 0` at the page root.
- Do not center an operational page inside an arbitrary page-level `max-width`. Empty side gutters reduce scanability and data density on wide displays.
- Limit line length on the explanatory text, empty state, or editorial region that needs it instead of constraining the whole page.
- Retain a page-level width cap only for an explicitly editorial or reading-focused layout, and record the readability rationale during review.

## Control shape and interaction

- Use only the `6px`, `8px`, and `12px` radius scale: 6 for compact controls, 8 for standard controls and compact surfaces, and 12 for major panels or dialogs.
- Use a full circle or pill only when the component's geometry or semantics requires it.
- Do not multiply a base radius, use `calc()` to derive larger radii, or let nested surfaces become progressively rounder.
- Do not lift, translate, scale, or add elevation on hover for static cards. Give hover motion only to elements that are actually actionable.

## Prefer flat color

- Default Monica page and component CSS to zero gradients. Use uniform palette-token or `color-mix(...)` fills when semantic tinting is useful.
- Do not use gradients on page shells, app bars, headers, hero or identity surfaces, KPI cards, panels, filters, tables, data groups, or status surfaces. Atmosphere or identity alone is not sufficient justification.
- Permit a gradient only when it functionally encodes a continuous data scale, or when the user or accepted prototype explicitly requires it. Document the purpose and verify that a flat treatment cannot communicate the same information.
- Keep theme-specific illustrative effects inside the named expressive theme. They must not leak into the default theme or first-party component CSS.
- Give a surface at most one primary decorative cue and, when necessary, one supporting cue. Count accent stripes, rings, strong borders, shadows, glows, textures, and corner ornaments as cues.
- Do not repeat top stripes or rings across every card, and do not stack multiple cues on an ordinary surface.
- Use semantic color to communicate state, selection, or meaning—not ambient ornament.

## Preserve data density

- Render repeated facts, properties, metrics, or records as rows, lists, definition groups, or tables—not as a grid of miniature cards.
- Use cards only for independently actionable objects or genuinely distinct content groups.
- Prefer separators, aligned columns, and typographic contrast over nested containers.

## Preserve typography and assets

- Vendor the prototype's declared font as local WOFF2 files and define local `@font-face` rules. Do not substitute a default system font silently.
- Verify the loaded font in browser computed styles and the network panel. If the exact font cannot be distributed, state the fallback and treat the fidelity loss as unresolved.
- Tune typography before adding decoration; wrong metrics alter wrapping, density, alignment, and the perceived quality of every surface.

## Verify fidelity

- Capture the prototype and implementation side by side at viewport widths `1440`, `929`, and `390` in both light and dark modes.
- Compare composition, content density, type metrics, spacing rhythm, surface roles, width utilization, radii, decoration count, and responsive reflow—not only colors.
- Assert that operational page roots consume the shell's available content width and that no page/component gradient exists without a recorded functional exception.
- Fix structural differences before polish. Re-run the comparison after global theme changes and after representative page changes.
- Reject the handoff while local fonts are missing, ordinary data is over-cardified, decoration exceeds the budget, operational width is wasted, or the implementation no longer matches the prototype's hierarchy.
