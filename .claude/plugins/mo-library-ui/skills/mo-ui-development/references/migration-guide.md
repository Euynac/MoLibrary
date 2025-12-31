# MudBlazor v8.9.0 Migration Guide

This document provides complete guidance for MudBlazor v8 API changes and migration patterns.

## Important: Version Differences

Claude Code's local knowledge base is based on v7.x, but the current codebase uses v8.9.0. This document lists key differences.

## 1. Async API Migration (Most Important)

### DialogService

```csharp
// v7 Old API (deprecated)
var dialog = DialogService.Show<MyDialog>();
var dialog = DialogService.Show<MyDialog>("Title", parameters);
var dialog = DialogService.Show(typeof(MyDialog));

// v8 New API (must use)
var dialog = await DialogService.ShowAsync<MyDialog>();
var dialog = await DialogService.ShowAsync<MyDialog>("Title", parameters);
var dialog = await DialogService.ShowAsync(typeof(MyDialog));
```

### MudDataGrid

```csharp
// v7 Old API (deprecated)
dataGrid.ExpandAllGroups();
dataGrid.CollapseAllGroups();
dataGrid.SetSelectedItem(item);
dataGrid.SetSelectedItems(items);

// v8 New API (must use)
await dataGrid.ExpandAllGroupsAsync();
await dataGrid.CollapseAllGroupsAsync();
await dataGrid.SetSelectedItemAsync(item);
await dataGrid.SetSelectedItemsAsync(items);
```

### MudThemeProvider

```csharp
// v7 Old API (deprecated)
var isDarkMode = await themeProvider.GetSystemPreference();
await themeProvider.WatchSystemPreference(OnSystemPreferenceChanged);
await themeProvider.SystemPreferenceChanged(isDarkMode);

// v8 New API (must use)
var isDarkMode = await themeProvider.GetSystemDarkModeAsync();
await themeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChanged);
await themeProvider.SystemDarkModeChangedAsync(isDarkMode);
```

## 2. Dialog Changes

### MudDialogInstance to IMudDialogInstance

```csharp
// v7
[CascadingParameter]
private MudDialogInstance MudDialog { get; set; }

// v8
[CascadingParameter]
private IMudDialogInstance MudDialog { get; set; }
```

### DialogOptions is Now Immutable

```csharp
[CascadingParameter]
private IMudDialogInstance MudDialog { get; set; }

private Task ToggleFullscreenAsync()
{
    var options = MudDialog.Options with
    {
        FullScreen = !(MudDialog.Options.FullScreen ?? false)
    };

    return MudDialog.SetOptionsAsync(options);
}
```

### DialogService Event Changes

```csharp
// v7 (removed)
DialogService.OnDialogInstanceAdded

// v8 (use instead)
DialogService.DialogInstanceAddedAsync
```

## 3. MudTheme Typography Changes

### Typography Class Renames

| v7 Class | v8 Class |
|----------|----------|
| `new Default()` | `new DefaultTypography()` |
| `new H1()` | `new H1Typography()` |
| `new H2()` | `new H2Typography()` |
| `new H3()` | `new H3Typography()` |
| `new H4()` | `new H4Typography()` |
| `new H5()` | `new H5Typography()` |
| `new H6()` | `new H6Typography()` |
| `new Subtitle1()` | `new Subtitle1Typography()` |
| `new Subtitle2()` | `new Subtitle2Typography()` |
| `new Body1()` | `new Body1Typography()` |
| `new Body2()` | `new Body2Typography()` |
| `new Button()` | `new ButtonTypography()` |
| `new Caption()` | `new CaptionTypography()` |
| `new Overline()` | `new OverlineTypography()` |

### Data Type Changes

**FontWeight and LineHeight are now strings:**

```csharp
// v7 (wrong in v8)
Typography = new Typography()
{
    Default = new Default()
    {
        FontWeight = 400,        // int - wrong
        LineHeight = 1.43,       // double - wrong
    }
}

// v8 (correct)
Typography = new Typography()
{
    Default = new DefaultTypography()
    {
        FontWeight = "400",      // string - correct
        LineHeight = "1.43",     // string - correct
    }
}
```

### Input Typography Removed

```csharp
// v7 (removed)
Typo.input

// v8 (use instead)
Typo.subtitle1
```

## 4. Palette Property Changes

### Spelling Corrections

```csharp
// v7 (wrong spelling)
BackgroundGrey = "#f7fafc"

// v8 (correct spelling)
BackgroundGray = "#f7fafc"
```

### Removed Properties

The following properties do not exist in v8:
- `ActionHover`
- `ActionSelected`
- `ActionSelectedHover`

### Available Action Properties

```csharp
PaletteLight = new PaletteLight()
{
    ActionDefault = "#667eea",
    ActionDisabled = "#e2e8f0",
    ActionDisabledBackground = "#f7fafc"
}
```

## 5. Shadow.Elevation Array Requirement

MudBlazor 8.9.0 requires Shadow.Elevation array to have **26 elements** (indices 0-25):

```csharp
// Wrong (25 elements)
Shadows = new Shadow()
{
    Elevation = new string[]
    {
        "none",                                    // index 0
        "0 2px 4px rgba(...)",                    // index 1
        // ... 23 more definitions ...
        "0 48px 96px rgba(...)"                   // index 24 (missing index 25)
    }
}

// Correct (26 elements)
Shadows = new Shadow()
{
    Elevation = new string[]
    {
        "none",                                    // index 0
        "0 2px 4px rgba(...)",                    // index 1
        // ... 23 more definitions ...
        "0 48px 96px rgba(...)",                  // index 24
        "0 50px 100px rgba(...)"                  // index 25 (required)
    }
}
```

**Reason:** MudThemeProvider accesses indices 0-25 to generate CSS variables. Missing any index causes runtime errors.

## 6. DataGrid Changes

### CellActions Required Properties

The following properties are now required:
- `SetSelectedItemAsync`
- `StartEditingItemAsync`
- `CancelEditingItemAsync`
- `ToggleHierarchyVisibilityForItemAsync`

### Renamed Properties

```csharp
// v7 -> v8
_classname -> Classname
_style -> Stylename
_tableStyle -> TableStyle
_tableClass -> TableClass
_headClassname -> HeadClassname
_footClassname -> FootClassname
_headerFooterStyle -> HeaderFooterStyle
CancelledEditingItem -> CanceledEditingItem
```

## 7. Input Component Changes

### MudInputAdornment

```csharp
// v7
MudInputAdornment.Edge

// v8
MudInputAdornment.Placement
```

### MudNumericField

```csharp
// Default InputMode changed
// v7: InputMode.numeric
// v8: InputMode.decimal
```

## 8. Radio, CheckBox, Switch Changes

```csharp
// v7
LabelPosition // for label positioning
Placement     // alternative
Checked       // boolean property

// v8
LabelPlacement // of type Placement (enum)
Value         // replaces Checked with generic T
```

### MudSwitch Rename

```csharp
// v7
SwitchLabelClassname

// v8
LabelClassName
```

## 9. MudChip Anchor Behavior

When `href` is specified, MudChip now renders a semantic anchor tag:
- Acts as a true anchor (not a button)
- Browser handles click and Enter key
- OnClick is disabled when href is set
- Close button not shown when href is set

## 10. MudMenu Changes

### Nested Menus

Menus can now be directly nested without additional setup. Nested menus inside another MudMenu render as MudMenuItem instead of MudButton.

### IconSize Removed

`IconSize` property was removed to align with Material Design.

## 11. MudBreadcrumbs Changes

`BreadcrumbItem` is now a record. Any inheriting class must be updated to a record:

```csharp
// v7 (class)
public class CustomBreadcrumbItem : BreadcrumbItem { }

// v8 (record)
public record CustomBreadcrumbItem : BreadcrumbItem { }
```

## 12. DropZone Spelling Corrections

```csharp
// v7 (typos)
GetTransactionOrignZoneIdentiifer
GetTransactionOrignZoneIdentifier
GetTransactionCurrentZoneIdentiifer
IsOrign

// v8 (corrected)
GetTransactionOriginZoneIdentifier
GetTransactionOriginZoneIdentifier
GetTransactionCurrentZoneIdentifier
IsOrigin
```

## 13. MudDrawer Behavior Change

The variant Temporary/Persistent now behaves non-responsively, meaning it's solely controlled by the Open parameter.

## 14. Internal API Changes

The following are now internal (use DI to access):
- `ResizeObserver` → Inject `IResizeObserver`/`IResizeObserverFactory`
- `EventListener` → Inject `IEventListener`/`IEventListenerFactory`
- `JsApiService` → Inject `IJsApiService`
- `JsEvent` → Inject `IJsEvent`/`IJsEventFactory`
- `ScrollListener` → Inject `IScrollListener`/`IScrollListenerFactory`
- `ScrollSpy` → Inject `IScrollSpy`/`IScrollSpyFactory`
- `ScrollManager` → Inject `IScrollManager`

## 15. Spelling Corrections

```csharp
// v7 -> v8
IIJSRuntimeExtentions -> IJSRuntimeExtensions
SortingAssistent -> SortingAssistant
TimeSeriesDiplayType -> TimeSeriesDisplayType
```

## 16. Method Renames

```csharp
// MudCheckBox, MudNumericField, MudSwitch
HandleKeyDown -> HandleKeyDownAsync
HandleKeyUp -> HandleKeyUpAsync
OnMouseWheel -> OnMouseWheelAsync
```

## 17. MudFormComponent Dispose Changes

```csharp
// v7
protected override void Dispose(bool disposing)

// v8
protected virtual ValueTask DisposeAsyncCore()
```

## 18. MudOverlay Changes

- Now moved to SectionOutlet with MudPopoverProvider in most cases
- Positioned statically by default
- Set `Absolute="true"` to avoid static positioning
- Unit tests need MudPopoverProvider for overlay interaction

## 19. MudToggleGroup Changes

```csharp
// v7
Rounded // property

// v8
// Removed - use CSS utility classes instead
Class="rounded-pill"
```

## Common Error Fixes

### Error: Method does not exist

Check if you should use the async version:
- `Show` → `ShowAsync`
- `Close` → `CloseAsync`
- `Expand` → `ExpandAsync`

### Error: Parameter setting invalid

Ensure no logic in parameter setter; use OnParametersSetAsync instead.

### Error: Theme not working

Check if using new PaletteLight/PaletteDark instead of old Palette.

### Error: 'Default' could not be found

Use `DefaultTypography` instead of `Default`.

### Error: Cannot convert type 'int' to 'string'

Convert numeric values to strings (e.g., `FontWeight = "400"`).

### Error: 'BackgroundGrey' does not exist

Use correct spelling: `BackgroundGray`.

## Migration Checklist

- [ ] Replace `DialogService.Show` with `ShowAsync`
- [ ] Replace `MudDialogInstance` with `IMudDialogInstance`
- [ ] Update Typography class names (add `Typography` suffix)
- [ ] Convert FontWeight/LineHeight to strings
- [ ] Fix palette property spellings
- [ ] Ensure Shadow.Elevation has 26 elements
- [ ] Update DataGrid renamed properties
- [ ] Replace deprecated sync methods with async versions
- [ ] Update internal API usage to DI injection
- [ ] Fix spelling corrections in method names
- [ ] Update MudBreadcrumbs inherited items to records
- [ ] Replace Checked with Value for Radio/CheckBox/Switch
- [ ] Replace LabelPosition with LabelPlacement

## Quick Reference

### Must Remember Rules

1. **All UI operations use async methods**
2. **Don't write logic in parameter setters**
3. **Use ParameterState for managing two-way binding**
4. **Components must support RTL**
5. **New components use v8 base classes**

### Version Check

```csharp
// Check MudBlazor version if needed
var assembly = typeof(MudComponentBase).Assembly;
var version = assembly.GetName().Version;
// Should be 8.9.0.x
```

---

**Tip:** When encountering uncertain APIs:
1. First check if there's an Async version
2. Use Grep to search for the component's latest implementation
3. Check the component's unit tests for correct usage
4. Reference this document's example code
