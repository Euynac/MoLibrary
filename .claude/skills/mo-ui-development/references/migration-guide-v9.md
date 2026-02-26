# MudBlazor v9.0.0 Migration Guide

This document provides complete guidance for migrating from MudBlazor v8 to v9.

## Important: Breaking Changes Overview

MudBlazor v9.0.0 introduces **significant breaking changes**. The compiler will NOT always catch these changes at compile time if you're using dynamic invocation or reflection. Ensure thorough testing after migration.

**Key Changes:**
1. **Converters**: Complete system redesign with new interfaces
2. **Obsolete APIs**: All deprecated v8 methods removed
3. **MudGlobal**: Theming properties removed
4. **MudFormComponent**: Must implement `GetDefaultConverter()`
5. **Method Renames**: Consistency improvements across components

## 1. Converters: Complete Rework

The converter system has been completely redesigned for better performance, type safety, and extensibility.

### Removed APIs

- `Converter<T, U>` class
- `Converter<T>` class
- `DefaultConverter` (old implementation)
- `BoolConverter` (old implementation)
- `DateConverter`
- `NumericConverter.AreEqual` method
- `Converters` static class

### New APIs

- `IConverter<TInput, TOutput>` interface
- `ICultureAwareConverter<TInput, TOutput>` interface
- `IReversibleConverter<TInput, TOutput>` interface
- `DefaultConverter<T>` (new implementation)
- `BoolConverter<T>` (new implementation)
- `RangeConverter<T>`
- `DeferredConverter<TInput, TOutput>`
- `EmptyConverter<TInput, TOutput>`
- `ConversionResult<T>` for error handling
- `ConverterExtensions` for fluent API
- `Conversions` static class for common conversions

### Migration Examples

**Custom Converters:**

❌ **Before (v8):**
```csharp
public class MyConverter : Converter<MyType>
{
    public MyConverter()
    {
        SetFunc = value => value?.ToString() ?? string.Empty;
        GetFunc = str => MyType.Parse(str);
    }
}
```

✅ **After (v9):**
```csharp
public class MyConverter : IReversibleConverter<MyType, string>
{
    public string Convert(MyType input)
    {
        return input?.ToString() ?? string.Empty;
    }

    public MyType ConvertBack(string input)
    {
        return MyType.Parse(input);
    }
}
```

**Inline Converters:**

❌ **Before (v8):**
```csharp
private Converter<ConverterElement?> _elementConverter = new Converter<ConverterElement?>
{
    SetFunc = value => value?.ToString(),
    GetFunc = text => new ConverterElement { Name = text }
};
```

✅ **After (v9):**
```csharp
private IConverter<ConverterElement?, string?> _elementConverter = Conversions
    .From((ConverterElement? value) => value?.ToString(),
          text => new ConverterElement { Name = text });
```

### GetDefaultConverter() Method Required

All components inheriting from `MudFormComponent` must now implement `GetDefaultConverter()` instead of setting the converter in the constructor.

❌ **Before (v8):**
```csharp
public class MyInput : MudFormComponent<MyType, string>
{
    public MyInput()
    {
        Converter = new DefaultConverter<MyType>
        {
            Culture = GetCulture,
            Format = GetFormat
        };
    }
}
```

✅ **After (v9):**
```csharp
public class MyInput : MudFormComponent<MyType, string>
{
    protected override IConverter<MyType?, string?> GetDefaultConverter()
    {
        return new DefaultConverter<MyType>
        {
            Culture = GetCulture,
            Format = GetFormat
        };
    }

    // Use GetConverter() to access the active converter
    private void SomeMethod()
    {
        var converter = GetConverter(); // Always returns non-null
    }
}
```

**Key Changes:**
- `Converter` parameter is now nullable
- If `Converter` is `null`, `GetDefaultConverter()` is called once and cached
- Use `GetConverter()` method (not the `Converter` property) to access the active converter
- Provides compile-time safety and better support for wrapper components

## 2. Remove All Obsolete/Deprecated Code

All code marked with `[Obsolete]` in v8 has been removed in v9.

### DialogService

❌ **Removed:**
- `Show(Type)` → use `ShowAsync(Type)`
- `Show<T>()` → use `ShowAsync<T>()`
- `ShowMessageBox()` → use `ShowMessageBoxAsync()`
- `ShowForm<T>()` → use `ShowFormAsync<T>()`
- `Close()` → use `CloseAsync()`

✅ **Correct (v9):**
```csharp
var dialog = await DialogService.ShowAsync<MyDialog>();
await DialogService.ShowMessageBoxAsync();
await DialogService.CloseAsync();
```

### MudDataGrid

❌ **Removed:**
- `ExpandAllGroups()` → use `ExpandAllGroupsAsync()`
- `CollapseAllGroups()` → use `CollapseAllGroupsAsync()`

✅ **Correct (v9):**
```csharp
await dataGrid.ExpandAllGroupsAsync();
await dataGrid.CollapseAllGroupsAsync();
```

### MudSelect

❌ **Removed:**
- `Clear()` → use `ClearAsync()`

✅ **Correct (v9):**
```csharp
await select.ClearAsync();
```

### MudTabs

❌ **Removed:**
- `ActivatePanel()` → use `ActivatePanelAsync()`

✅ **Correct (v9):**
```csharp
await tabs.ActivatePanelAsync(panel);
```

## 3. MudGlobal: Theming Properties Removed

All theming-related properties have been removed from `MudGlobal`. Use CSS variables, theme configuration, or explicit component parameters instead.

❌ **Removed:**
- `MudGlobal.Rounded`
- `MudGlobal.ButtonDefaults.Color`
- `MudGlobal.ButtonDefaults.Variant`
- `MudGlobal.InputDefaults.ShrinkLabel`
- `MudGlobal.InputDefaults.Variant`
- `MudGlobal.InputDefaults.Margin`
- `MudGlobal.LinkDefaults.Color`
- `MudGlobal.LinkDefaults.Typo`
- `MudGlobal.LinkDefaults.Underline`
- `MudGlobal.GridDefaults.Spacing`
- `MudGlobal.StackDefaults.Spacing`
- `MudGlobal.PopoverDefaults.Elevation`

✅ **Migration:**
```csharp
// Wrong (removed in v9)
MudGlobal.ButtonDefaults.Color = Color.Primary;
MudGlobal.InputDefaults.Variant = Variant.Outlined;

// Correct (v9) - Use component parameters
<MudButton Color="Color.Primary">Button</MudButton>
<MudTextField Variant="Variant.Outlined" />
```

## 4. MudFormComponent & MudBaseInput: API Changes

### Method Naming Changes

Several methods have been renamed for consistency:

- `Reset()` → `ResetAsync()`
- `Validate()` → `ValidateAsync()`
- `ReadValue()` → `ReadValue` (property-style, no parentheses)

### WriteValueAsync Renamed to SetValueCoreAsync

❌ **Before (v8):**
```csharp
protected virtual Task WriteValueAsync(T? value)
{
    _value = value;
    return Task.CompletedTask;
}
```

✅ **After (v9):**
```csharp
protected override Task SetValueCoreAsync(T? value)
{
    _value = value;
    return Task.CompletedTask;
}
```

### SetValueAsync Renamed to SetValueAndUpdateTextAsync

❌ **Before (v8):**
```csharp
await SetValueAsync(value, updateText: true, force: false);
```

✅ **After (v9):**
```csharp
await SetValueAndUpdateTextAsync(value, updateText: true, force: false);
```

### SetTextAsync Renamed to SetTextCoreAsync

❌ **Before (v8):**
```csharp
protected Task SetTextAsync(string? text);
```

✅ **After (v9):**
```csharp
protected Task SetTextCoreAsync(string? text);
```

## 5. MudSelect: SelectedValues Type Change

`SelectedValues` is now `IReadOnlyCollection<T>` instead of `ICollection<T>`.

❌ **Before (v8):**
```csharp
ICollection<T> SelectedValues { get; set; }
```

✅ **After (v9):**
```csharp
IReadOnlyCollection<T> SelectedValues { get; set; }
```

## 6. EventListener / EventManager Removed

The entire event management infrastructure has been removed:

❌ **Removed:**
- `IEventListener` / `EventListener`
- `IEventListenerFactory` / `EventListenerFactory`
- `IEventManager`
- `WebEventJsonContext`

## 7. Range<T> and DateRange: Immutable

`Range<T>` and `DateRange` properties are now read-only (init-only). Create new instances instead of mutating existing ones.

❌ **Before (v8):**
```csharp
var range = new Range<int> { Start = 1, End = 10 };
range.Start = 5; // Allowed
```

✅ **After (v9):**
```csharp
var range = new Range<int> { Start = 1, End = 10 };
// range.Start = 5; // Compile error

// Create new instance instead
range = range with { Start = 5 };
```

## Migration Checklist

- [ ] Replace all custom converters with new interface implementations
- [ ] Implement `GetDefaultConverter()` for custom form components
- [ ] Remove all synchronous method calls (use async versions)
- [ ] Remove `MudGlobal` theming property usage
- [ ] Update `WriteValueAsync` to `SetValueCoreAsync`
- [ ] Update `SetValueAsync(T, bool, bool)` to `SetValueAndUpdateTextAsync`
- [ ] Update `SetTextAsync` to `SetTextCoreAsync`
- [ ] Change `ICollection<T>` to `IReadOnlyCollection<T>` for `MudSelect.SelectedValues`
- [ ] Remove `EventListener` / `EventManager` usage
- [ ] Treat `Range<T>` and `DateRange` as immutable

## Quick Reference

### Version Check

```csharp
var assembly = typeof(MudComponentBase).Assembly;
var version = assembly.GetName().Version;
// Should be 9.0.0.x
```

### Common Patterns

**Accessing Active Converter:**
```csharp
// ❌ Don't access Converter directly if it might be null
var converter = Converter; // May be null!

// ✅ Use GetConverter() which handles the fallback
var converter = GetConverter(); // Always returns non-null
```

**Creating Inline Converters:**
```csharp
private IConverter<MyType?, string?> _converter = Conversions
    .From((MyType? value) => value?.ToString(),
          text => MyType.Parse(text));
```

---

**For v8 migration details, see `migration-guide-v8.md`.**
