# Monica Precision Default Design Language

Use this contract for the default theme, global visual-language changes, and prototype handoff. Treat a supplied prototype as an implementation contract for composition, density, typography, spacing, and surface roles—not as loose inspiration.

## Define the point of view

- State a one-sentence thesis tied to the page's subject, primary decisions, and operator context before choosing visuals. A system-readiness page and a module-discovery workbench should not inherit the same generic dashboard composition.
- Plan four things deliberately: palette roles, typography roles and fallbacks, layout rhythm, and one signature treatment. Explain what each contributes to the task.
- Choose one memorable, subject-specific treatment backed by the accepted prototype—for example an identity/readiness region. Give supporting regions enough color, contrast, and interaction feedback to feel alive while preserving a clear hierarchy around that signature.
- Reject generic AI-dashboard habits such as interchangeable purple atmosphere, glass panels, ubiquitous glow, oversized marketing heroes, pill overload, or a grid of rounded cards when they do not express Monica data or workflows.

## Build the hierarchy first

1. Establish information priority with type scale, weight, line height, whitespace, alignment, and density.
2. Name each surface role before styling it: canvas, section, ordinary surface, interactive/raised surface, semantic surface, or visualization.
3. Use tonal contrast and spacing for grouping. Add decoration only after the hierarchy reads correctly without it.
4. Keep ordinary surfaces structurally simple. Use low token-based elevation or border/shadow response when it improves separation or spatial focus; reserve the strongest elevation for overlays, menus, dialogs, or content that genuinely sits above another layer.

## Use the available width

- Let operational dashboards, workbenches, administration pages, and diagnostics pages consume the full width provided by their shell. Use `width: 100%` and `min-width: 0` at the page root.
- Do not center an operational page inside an arbitrary page-level `max-width`. Empty side gutters reduce scanability and data density on wide displays.
- Limit line length on the explanatory text, empty state, or editorial region that needs it instead of constraining the whole page.
- Retain a page-level width cap only for an explicitly editorial or reading-focused layout, and record the readability rationale during review.

## Control shape and interaction

- Use only the `6px`, `8px`, and `12px` radius scale: 6 for compact controls, 8 for standard controls and compact surfaces, and 12 for major panels or dialogs.
- Use a full circle or pill only when the component's geometry or semantics requires it.
- Do not multiply a base radius, use `calc()` to derive larger radii, or let nested surfaces become progressively rounder.
- Give actionable controls and interactive rows or cards clear hover, focus, pressed, and selected feedback. Short token-based changes to color, border, and shadow are welcome; reserve a very small transform for pressed feedback when it reinforces the control's physical response.
- Informational grouped surfaces may use a subtle border, tonal, or low-shadow hover response to aid spatial focus. Do not translate, scale, change the cursor, or otherwise imply an action that does not exist.
- Keep motion purposeful and moderate. Prefer one short, coordinated transition over scattered effects; avoid large travel, bouncing, and repeated flourishes. Continuous motion must encode active progress or changing data, remain nonessential and pausable, and stop under reduced motion.
- Honor `prefers-reduced-motion`. Remove nonessential transforms, parallax, loops, and decorative animation; make necessary state changes immediate or near-immediate, preserve their visible meaning, and never use motion as the only cue.

## Reuse the shared card contract

- Standard `MudCard` instances receive the baseline theme-aware border/shadow hover automatically. Add `mo-card-surface` only when a genuine independent card or grouped evidence surface uses native markup; it provides the same response without changing the cursor or translating non-actionable content.
- Keep tables, rows, navigation items, visualizations, dialogs, overlays, structural panels, and nested facts outside this hook. Their interaction or layout semantics require dedicated states.
- For a semantic KPI rail, set `data-mo-card-tone` to `neutral`, `primary`, `secondary`, `info`, `success`, `warning`, or `error`, then add `<span class="mo-card-surface__rail" aria-hidden="true"></span>` as a direct child. The shared rail is `3px` wide with a `12px` block inset, matching the System Info reference treatment.
- Let `--mo-card-accent` resolve from MudBlazor palette roles. Component CSS may consume it for matching icons, badges, or subtle tonal fills; do not recreate page-private accent aliases or full-height colored borders.
- Use the real rail child instead of `::before` or `::after`. Expressive themes may already own both pseudo-elements, and table cells use pseudo-elements for responsive labels.
- Themes may override the shared `--mo-card-hover-*` and `--mo-card-rail-*` variables when their visual identity requires it. Keep the default geometry unless browser comparison demonstrates a specific need.
- Keep `mo-self-painted` separate: it protects component-owned MudCard or MudPaper paint from broad theme selectors and does not opt a surface into hover or an accent rail.

## Use color and effects deliberately

- Use solid or tonal palette-token surfaces as the default for page shells, app bars, navigation, ordinary cards, KPI tiles, panels, filters, tables, data groups, and status surfaces. Subtle token-based borders, shadows, and interaction responses may add separation and depth without turning every region into a floating card.
- Establish one focused, prototype-backed signature system using a token-based gradient, glow, or pattern. Concentrate it in the identity/readiness region and repeat it only in limited, non-competing placements when the prototype uses that recurrence to connect the hierarchy; do not promote an ordinary KPI or data card into a signature surface.
- Permit gradients in visualizations only when they encode a continuous scale. Document the mapping and preserve legibility for users who cannot rely on color alone.
- Keep named expressive-theme illustration inside theme-owned selectors; first-party component CSS must preserve the surface-role contract.
- Give a surface at most one primary decorative cue and, when necessary, one supporting cue. Count accent stripes, rings, strong borders, shadows, glows, textures, and corner ornaments as cues.
- Do not repeat top stripes or rings across every card, and do not stack multiple cues on an ordinary surface.
- Use semantic color to communicate state, selection, or meaning—not ambient ornament.

## Preserve data density

- Render repeated facts, properties, metrics, or records as rows, lists, definition groups, or tables—not as a grid of miniature cards.
- Use cards only for independently actionable objects or genuinely distinct content groups.
- Prefer separators, aligned columns, and typographic contrast over nested containers.

## Preserve typography and assets

- Define distinct roles for interface text, hierarchy/display text when the design needs it, and diagnostic/code text. Keep monospace focused on technical evidence instead of using it as a general UI affectation.
- Vendor the prototype's declared font as local WOFF2 files and define local `@font-face` rules. Declare an intentional offline-capable fallback stack, including CJK coverage where localized content requires it; do not substitute a system font silently.
- Verify the loaded font and fallback path in browser computed styles and the network panel with external network access unavailable. If the exact font cannot be distributed, state the fallback and treat the fidelity loss as unresolved.
- Tune typography before adding decoration; wrong metrics alter wrapping, density, alignment, and the perceived quality of every surface.

## Preserve palette meaning

- Assign explicit roles for canvas, surface, border, primary text, secondary text, identity, focus, runtime, success, warning, error, and information.
- Keep dark mode readable and layered rather than uniformly near-black. Adjacent structural surfaces must remain distinguishable without relying on shadows or glow.
- Give semantic states visibly different token treatments when users must scan or compare them. Reinforce color with text, icon, shape, or position instead of making color the sole signal.

## Verify fidelity

- Capture the prototype and implementation side by side at viewport widths `1440`, `929`, and `390` in both light and dark modes.
- Compare composition, content density, type metrics, spacing rhythm, surface roles, width utilization, radii, decoration count, and responsive reflow—not only colors.
- Assert that operational page roots consume the shell's available content width. Record the signature system, its limited placements, and every continuous-data gradient; verify ordinary surfaces have clear roles and proportionate interaction feedback.
- Fix structural differences before polish. Re-run the comparison after global theme changes and after representative page changes.
- Ask whether the result could be relabeled as an unrelated AI administration product without meaningful visual changes. If yes, strengthen the subject-specific thesis, layout, or signature treatment without adding more decoration.
- Reject the handoff while local fonts or fallbacks are unresolved, palette roles or semantic states collapse, ordinary data is over-cardified, interaction feedback is missing or excessive, reduced-motion behavior is broken, decoration exceeds the budget, operational width is wasted, or the implementation no longer matches the prototype's hierarchy.
