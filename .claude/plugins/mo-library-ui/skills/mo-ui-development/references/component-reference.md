# MudBlazor v8.9.0 Component Reference

This document provides a quick reference for MudBlazor v8 components.

## Component Categories

### 1. Layout Components

- **MudContainer** - Responsive container
- **MudGrid/MudItem** - 12-column grid system
- **MudPaper** - Material Design paper effect
- **MudCard** - Card container
- **MudDrawer** - Side drawer
- **MudLayout** - Page layout framework
- **MudMainContent** - Main content area
- **MudSpacer** - Flexible space
- **MudDivider** - Divider line
- **MudBreakpointProvider** - Responsive breakpoints

### 2. Navigation Components

- **MudAppBar** - Application bar
- **MudBreadcrumbs** - Breadcrumb navigation
- **MudLink** - Link
- **MudMenu** - Dropdown menu
- **MudNavMenu/MudNavLink** - Navigation menu
- **MudPagination** - Pagination
- **MudTabs/MudTabPanel** - Tab panels
- **MudStepper/MudStep** - Stepper (v8 new)
- **MudSpeedDial** - Speed dial button

### 3. Input Components

- **MudTextField** - Text input
- **MudNumericField** - Numeric input
- **MudSelect** - Dropdown select
- **MudAutocomplete** - Autocomplete
- **MudCheckBox** - Checkbox
- **MudRadio/MudRadioGroup** - Radio buttons
- **MudSwitch** - Switch toggle
- **MudSlider** - Slider
- **MudRating** - Rating
- **MudToggleGroup/MudToggleItem** - Toggle group (v8 new)
- **MudColorPicker** - Color picker
- **MudDatePicker** - Date picker
- **MudTimePicker** - Time picker
- **MudDateRangePicker** - Date range picker
- **MudMask** - Input mask
- **MudFileUpload** - File upload

### 4. Data Display Components

- **MudTable** - Basic table
- **MudDataGrid** - Advanced data grid (supports drag-drop sorting)
- **MudTreeView/MudTreeViewItem** - Tree view
- **MudList/MudListItem** - List
- **MudChip/MudChipSet** - Chips
- **MudBadge** - Badge
- **MudAvatar** - Avatar
- **MudTooltip** - Tooltip
- **MudCarousel** - Carousel
- **MudTimeline** - Timeline
- **MudChat/MudChatBubble** - Chat component (v8 new)

### 5. Feedback Components

- **MudAlert** - Alert
- **MudSnackbar** - Snackbar notification
- **MudDialog** - Dialog
- **MudProgressCircular** - Circular progress
- **MudProgressLinear** - Linear progress
- **MudSkeleton** - Skeleton loading
- **MudOverlay** - Overlay
- **MudBackdrop** - Backdrop

### 6. Button Components

- **MudButton** - Standard button
- **MudIconButton** - Icon button
- **MudFab** - Floating action button
- **MudButtonGroup** - Button group
- **MudToggleIconButton** - Toggle icon button

### 7. Utility Components

- **MudIcon** - Icon
- **MudText** - Text typography
- **MudHidden** - Responsive hiding
- **MudFocusTrap** - Focus trap
- **MudVirtualize** - Virtual scrolling
- **MudSwipeArea** - Swipe area
- **MudScrollToTop** - Scroll to top
- **MudMessageBox** - Message box
- **MudContextualActionBar** - Contextual action bar (v8 new)

## Common Code Examples

### Form Example

```razor
<MudForm @ref="form" @bind-IsValid="@isValid">
    <MudTextField T="string"
                  Label="Username"
                  @bind-Value="username"
                  Required="true"
                  RequiredError="Username is required" />

    <MudTextField T="string"
                  Label="Password"
                  @bind-Value="password"
                  InputType="InputType.Password"
                  Required="true" />

    <MudButton ButtonType="ButtonType.Submit"
               Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="!isValid">
        Submit
    </MudButton>
</MudForm>
```

### Data Grid Example

```razor
<MudDataGrid T="Person"
             Items="@people"
             Filterable="true"
             SortMode="@SortMode.Multiple"
             DragDropColumnReordering="true">
    <Columns>
        <PropertyColumn Property="x => x.Name" Title="Name" />
        <PropertyColumn Property="x => x.Age" Title="Age" />
        <PropertyColumn Property="x => x.Email" Title="Email" />
        <TemplateColumn Title="Actions">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Edit"
                               Size="Size.Small"
                               OnClick="@(() => EditPerson(context.Item))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

### Dialog Example

```csharp
@code {
    private async Task ShowDialogAsync()
    {
        var parameters = new DialogParameters<MyDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete?" },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error }
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small
        };

        var dialog = await DialogService.ShowAsync<MyDialog>("Confirm Delete", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Execute delete operation
        }
    }
}
```

### Toggle Group Example (v8 New)

```razor
<MudToggleGroup T="string" @bind-Value="selectedValue">
    <MudToggleItem Value="@("option1")">Option 1</MudToggleItem>
    <MudToggleItem Value="@("option2")">Option 2</MudToggleItem>
    <MudToggleItem Value="@("option3")">Option 3</MudToggleItem>
</MudToggleGroup>
```

### Stepper Example (v8 New)

```razor
<MudStepper @ref="stepper">
    <MudStep Title="Step 1">
        <ChildContent>Step 1 content</ChildContent>
    </MudStep>
    <MudStep Title="Step 2">
        <ChildContent>Step 2 content</ChildContent>
    </MudStep>
    <MudStep Title="Complete">
        <ChildContent>Complete content</ChildContent>
    </MudStep>
</MudStepper>
```

### DataGrid Drag-Drop Example (v8 New)

```razor
<MudDataGrid T="MyModel"
             Items="@items"
             DragDropColumnReordering="true"
             ColumnsPanelReordering="true"
             DragIndicatorIcon="@Icons.Material.Filled.DragIndicator"
             DropAllowedClass="drop-allowed"
             DropNotAllowedClass="drop-not-allowed">
    <!-- Column definitions -->
</MudDataGrid>
```

## Common Component Properties

### All MudComponents Support

- **Class** - CSS class name
- **Style** - Inline styles
- **UserAttributes** - Custom HTML attributes
- **@ref** - Component reference

### Form Component Common Properties

- **@bind-Value** - Two-way binding
- **Label** - Label
- **Placeholder** - Placeholder
- **Required** - Required
- **RequiredError** - Required error message
- **Disabled** - Disabled
- **ReadOnly** - Read only
- **Error** - Has error
- **ErrorText** - Error text
- **HelperText** - Helper text
- **Variant** - Variant (Text/Filled/Outlined)
- **Margin** - Margin (None/Dense/Normal)

### Color Property Values

- Primary
- Secondary
- Tertiary
- Info
- Success
- Warning
- Error
- Dark
- Light
- Transparent
- Inherit
- Surface

### Size Property Values

- Small
- Medium
- Large

### Variant Property Values

- Text
- Filled
- Outlined

## Search Tips

### Find Component Implementation

```bash
# Find component definition
Glob: "**/Mud{ComponentName}.razor"
Glob: "**/Mud{ComponentName}.razor.cs"

# Find component tests
Glob: "**/{ComponentName}Tests.cs"

# Find component styles
Glob: "**/_mud{componentname}.scss"
```

### Find Component Usage

```bash
# Find examples in documentation
Glob: "**/Examples/{ComponentName}*.razor"

# Find usage in tests
Grep: "<Mud{ComponentName}"
```

## Important Notes

1. **Component naming rule**: All components start with "Mud" prefix
2. **Parameter binding**: Use @bind-Value for two-way binding
3. **Event callbacks**: Use EventCallback<T> type
4. **Async operations**: Prefer methods with Async suffix
5. **Style customization**: Through Class and Style properties, or use theme system

## Quick Component Location

If unsure about component location, use these path patterns:

- Component definition: `src/MudBlazor/Components/{Category}/Mud{ComponentName}.razor`
- Component logic: `src/MudBlazor/Components/{Category}/Mud{ComponentName}.razor.cs`
- Component tests: `src/MudBlazor.UnitTests/Components/{ComponentName}Tests.cs`
- Component styles: `src/MudBlazor/Styles/components/_{componentname}.scss`

Categories include:
AppBar, Avatar, Badge, Breadcrumbs, Button, Card, Carousel, Chart, Checkbox, Chip, ColorPicker, DataGrid, DatePicker, Dialog, Divider, Drawer, ExpansionPanel, Field, FileUpload, Form, Grid, Hidden, Highlighter, Icon, Input, Layout, Link, List, Menu, MessageBox, NavMenu, Overlay, Pagination, Paper, Popover, Progress, Radio, Rating, ScrollToTop, Select, Skeleton, Slider, Snackbar, SpeedDial, Stepper, SwipeArea, Switch, Table, Tabs, TextField, Timeline, TimePicker, ToggleButton, Tooltip, TreeView, Typography, Virtualize
