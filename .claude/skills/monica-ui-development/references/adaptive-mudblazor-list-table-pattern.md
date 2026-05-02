# Adaptive MudBlazor List/Table Pattern

Use this reference when a Monica UI list/table has long values that crowd columns, overlap adjacent content, or create an unnecessary initial horizontal scrollbar.

The goal is not to restyle MudBlazor. The goal is a predictable, theme-first layout where important operational columns remain readable and long identifiers/names truncate safely with full-value access.

## When to Use This Pattern

Use this pattern for dense `MudTable` or `MudDataGrid` pages when:

- Long keys, names, instance IDs, cron expressions, timestamps, or statuses can overlap neighboring columns.
- A page should fit the initial viewport better, but still allow horizontal scroll if the viewport is truly too narrow.
- The user asks for adaptive clipping/ellipsis instead of hard-coded string truncation.
- Existing `table-layout: fixed`, inline `max-width`, or ad-hoc widths are not enough.
- You need alignment consistency between badges, icons, plain text, and action buttons.

Prefer a smaller fix for ordinary card/content overflow. For example, a System Info version/hash field usually only needs a scoped wrapper with `min-width: 0`, `max-width: 100%`, `overflow-wrap: anywhere`, `word-break: break-word`, and a full-value tooltip.

## Non-Negotiable Rules

1. Do not hard-code visible substrings such as `InstanceId[..8] + "..."` for layout. Render the full value and let CSS ellipsis control the visible text.
2. Put full values in `MudTooltip` when clipping is possible.
3. Use scoped `.razor.css`; do not add broad/global CSS for one table.
4. Keep visuals theme-first. Use MudBlazor defaults and `var(--mud-palette-*)`; do not introduce gradients, custom colors, fonts, or decorative effects for a layout fix.
5. Avoid new shared copy components or broad copy affordances unless the user explicitly asks. If copy is requested, keep it targeted and MudBlazor-native.
6. Verify in the browser. Builds do not catch overlap, baseline drift, or empty-row centering issues.

## MudTable CSS-Grid Layout

Wrap the table in a page-specific container that owns the column contract:

```razor
<div class="job-definitions-table-wrapper">
    <MudTable Items="@_definitions" Class="job-definitions-table">
        ...
    </MudTable>
</div>
```

```css
.job-definitions-table-wrapper {
    --table-columns: minmax(12rem, 1.2fr) minmax(14rem, 1.4fr) minmax(12rem, 1fr) minmax(9rem, auto) minmax(11rem, auto);
    overflow-x: auto;
}

.job-definitions-table-wrapper ::deep .mud-table-root {
    display: grid;
    grid-template-columns: var(--table-columns);
    min-width: 0;
}

.job-definitions-table-wrapper ::deep thead,
.job-definitions-table-wrapper ::deep tbody,
.job-definitions-table-wrapper ::deep tr {
    display: contents;
}

.job-definitions-table-wrapper ::deep th,
.job-definitions-table-wrapper ::deep td {
    display: flex;
    align-items: center;
    min-width: 0;
    overflow: hidden;
}
```

### Column Sizing Guidance

- Give long text columns flexible `minmax(..., fr)` tracks.
- Give operational columns enough fixed/auto minimum width so they do not disappear: cron expression, next/last execution time, status, and actions are common examples.
- Keep the action column wide enough for all visible buttons. Do not solve overlap by hiding actions.
- Tune each table shape separately. Recurring definitions, trigger definitions, instances, and project units usually need different `--table-columns`.
- Use page-level full width (`MaxWidth="MaxWidth.False"`) for dense list pages when the default container is the real bottleneck.

## Ellipsis Value Wrappers

Inside any cell that can truncate, wrap the text/link/tooltip target with a block-level value class:

```razor
<MudTooltip Text="@context.JobKey">
    <span class="job-definitions-table__ellipsis">@context.JobKey</span>
</MudTooltip>
```

```css
.job-definitions-table__ellipsis {
    display: block;
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
```

Important details:

- `min-width: 0` is required on both the flex cell and the value wrapper.
- `display: block` on the value wrapper avoids inline elements ignoring the available width.
- If the clipped value is a link, put the ellipsis class on the link or on a block wrapper inside the link.
- If `MudTooltip` introduces an extra wrapper, inspect the DOM and ensure the element receiving ellipsis has the constrained width.

## MudDataGrid Notes

For `MudDataGrid`, prefer the same wrapper principle but adapt selectors to the actual runtime DOM. Add `HeaderClass` and `CellClass` where useful, then style those classes through the scoped wrapper with `::deep`.

```razor
<MudDataGrid Items="@_units" Class="project-units-monitor-grid">
    <Columns>
        <PropertyColumn Property="x => x.UnitName" HeaderClass="project-units-monitor-grid__name" CellClass="project-units-monitor-grid__name" />
    </Columns>
</MudDataGrid>
```

Before writing selectors, inspect the live DOM or generated scoped CSS. MudBlazor class names and internal wrappers can differ from assumptions.

## Empty Rows Under CSS-Grid Tables

MudTable empty rows can become left-aligned or span only one grid column after converting rows to `display: contents`. Fix the generated empty row explicitly:

```css
.job-definitions-table-wrapper ::deep tr:has(.mud-table-empty-row) {
    display: contents;
}

.job-definitions-table-wrapper ::deep .mud-table-empty-row {
    grid-column: 1 / -1;
    width: 100%;
    justify-content: center;
    text-align: center;
}
```

Verify this visually for every tab/filter that can be empty.

## Alignment for Badges, Icons, and Plain Text

- Keep cells as flex containers with `align-items: center` so badges, icon buttons, and plain text share a consistent vertical line.
- If a badge/button column needs a slight left/right nudge, scope the tweak to the actual badge/button wrapper or a `:has(...)` selector.
- Do not apply padding shifts to the whole column when plain `-` values are also rendered there; that makes empty values look misaligned.
- Action stacks should use `align-items: center` and should not wrap unexpectedly at the target viewport.

## Verification Checklist

Before acceptance or PR:

1. Build affected projects with zero warnings.
2. Open the page at normal width and confirm there is no initial unwanted horizontal scrollbar.
3. Resize narrower and confirm true overflow scroll appears only when necessary.
4. Confirm long key/name/ID values show ellipsis, not overlap.
5. Hover clipped values and confirm the full value appears in `MudTooltip`.
6. Confirm operational columns remain readable: cron, next/last time, status, duration, actions.
7. Confirm badges, icons, plain text, and actions align on one visual row.
8. Check empty states for centered full-width messages.
9. Save screenshots for populated and empty/narrow cases when the task is UI-facing.
10. Inspect the diff for overreach: no unrelated styling, no new shared copy components, no hard-coded truncation, and no broad global CSS.

## Common Pitfalls

- `display: contents` makes layout work but removes the row box; empty rows need explicit spanning, and row-level styles such as hover/background must be re-applied to cells, for example with `tr:hover td`.
- Ellipsis fails without `min-width: 0` on flex/grid children.
- Tooltip wrappers can become the real width owner; inspect the DOM if ellipsis does not appear.
- Inline elements do not reliably truncate; use block-level wrappers.
- `table-layout: fixed` alone often hides content rather than solving the column contract. The CSS-grid pattern can also conflict with `FixedHeader="true"` because sticky header positioning needs stable parent boxes; verify fixed-header behavior before combining the two.
- Overly small min widths on operational columns cause status/actions to disappear first, which is usually worse than truncating keys/names.
- CSS isolation will not style MudBlazor internals unless the selector crosses from a real scoped wrapper with `::deep`.
