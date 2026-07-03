# Configuration Status Icon Slot Design

## Context

The Configuration State page shows two compact status icons beside the page title:

- Runtime validation status
- Runtime reload status

The current implementation uses `MudBadge` around `MudIconButton`. Previous fixes moved the badge back to the top-right position and then tuned the badge offset with component-local CSS. That made the result better, but the design is still brittle because it depends on MudBlazor's internal badge wrapper classes and still risks a visually clipped top edge.

The selected direction is to keep the count visually attached to the icon, but own the geometry directly instead of tuning `MudBadge` after render.

## Decision

Create a small reusable Configuration UI component for title-row status icons named `ConfigurationStatusIconButton`.

The component owns the icon slot and count badge geometry. It renders a `MudIconButton` plus a plain count badge element inside a fixed-size wrapper. It does not use `MudBadge`.

This is a visual-shell component only. It does not fetch data, compute status, or know what validation or reload means.

## Component Contract

The component accepts simple rendering and interaction inputs:

- `Icon`
- `Color`
- `Count`
- `ShowCount`
- `Disabled`
- `Tooltip`
- `AriaLabel`
- `OnClick`

The parent remains responsible for state and behavior:

- `ConfigurationStatePage` continues to compute the runtime validation icon, color, count, tooltip, and popover action.
- `ConfigurationReloadStatusButton` continues to load reload status, compute drift count, choose icon/color, and open or reload its popover.

## Layout

The component uses a fixed outer slot, for example about `38px x 34px`, large enough to contain both the icon button and the count badge.

The slot:

- Uses `position: relative`
- Centers the icon button
- Reserves enough top/right space for the count
- Avoids placing the badge outside the slot

The count badge:

- Is a plain positioned element
- Sits at the top-right inside the slot
- Uses stable dimensions and line height
- Uses MudBlazor palette CSS variables or approved Monica theme tokens
- Does not depend on MudBlazor internal badge class names

For larger counts, the visual label should cap at a stable value such as `99+` so it cannot grow into nearby title-row icons.

## Interaction And Accessibility

The tooltip should describe the whole status action.

The `MudIconButton` keeps keyboard accessibility and the `aria-label`. Clicking the icon triggers the existing action. The badge does not introduce a separate focus target.

The count badge uses `pointer-events: none` so it cannot steal pointer behavior from the button. Keyboard accessibility remains anchored to the icon button.

## Integration Scope

Replace only the two title-row status badge usages:

- Runtime validation badge in `ConfigurationStatePage.razor`
- Runtime reload badge in `ConfigurationReloadStatusButton.razor`

Remove the current badge-position overrides from:

- `ConfigurationStatePage.razor.css`
- `ConfigurationReloadStatusButton.razor.css`

Do not change the popover contents, reload status logic, runtime validation logic, or save/reload conflict behavior.

## Verification

Verify the UI at `http://localhost:5028/configuration/state`.

Badge count cases:

- No count
- `1`
- Two digits such as `18`
- A capped large count such as `99+`

Status combinations:

- Validation count only
- Reload count only
- Both status icons present
- No counts visible

Interaction checks:

- Runtime validation popover opens from the validation icon.
- Reload status popover opens from the reload icon.
- Tooltip and keyboard behavior remain intact.

Viewport checks:

- Current desktop viewport
- Narrow viewport where the header may wrap

Build and validation:

- `dotnet build 'D:\Code\MoLibrary\Monica.Configuration.UI\Monica.Configuration.UI.csproj' -m`
- `dotnet build 'D:\Code\MoLibrary\Monica.slnx' -m`
- Run UI theme-token validation for changed CSS. If the full repo-wide validator still stalls while walking local `.tmp` source caches, run the same validator logic scoped to changed CSS files and report that limitation explicitly.

## Non-Goals

This design does not change runtime validation diagnostics, reload status data, manual reload behavior, save preflight behavior, or popover contents.

This design does not introduce a generic Monica-wide badge replacement. It is scoped to the Configuration State title-row status icons.
