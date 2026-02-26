---
name: mo-ui-development
description: This skill should be used when the user asks to "create UI component", "build Blazor page", "add MudBlazor component", "style MudBlazor", "fix CSS isolation", "use ::deep selector", "customize theme", "support dark mode", "migrate MudBlazor v9", "use OnAfterRenderAsync", "create UI module", "module file structure", "UI folder structure", "refactor Minimal API", "persist UI state", "browser storage", "save table state", "IMoBrowserStorage", "localStorage", "sessionStorage", "implement converter", "GetDefaultConverter", or needs guidance on Blazor component lifecycle, MudBlazor styling patterns, CSS isolation, theme customization, offline UI requirements, browser storage patterns, converter implementation, or MoFramework UI module structure in the Monica framework.
version: 1.0.0
---

# Monica UI Development Guide

This skill provides essential guidance for developing Blazor UI components with MudBlazor 9.0.0 in the Monica framework.

## Critical Rules

### 1. CSS Isolation Requirements

**Never use `<style>` tags.** Always use CSS isolation with `.razor.css` files.

**CSS isolation only applies to HTML elements, NOT to Razor components.** MudBlazor components generate elements at runtime, so they won't receive the CSS isolation attribute.

To style MudBlazor components:
1. Wrap the component with a container element
2. Use the `::deep` selector pattern

```razor
<!-- Correct Pattern -->
<div class="table-wrapper">
    <MudTable Items="@items">
        ...
    </MudTable>
</div>
```

```css
/* In .razor.css file */
.table-wrapper ::deep .mud-table {
    background-color: var(--mud-palette-surface);
}

.table-wrapper ::deep tr.mud-selected {
    background-color: var(--mud-palette-action-default-hover) !important;
}
```

**Common mistake:** Using `::deep` without a wrapper element will not work.

For detailed CSS isolation fixing workflow, see `references/css-isolation-fix-workflow.md`.

### 2. MudBlazor Icon Property

Always use the `@` prefix when referencing icons:

```razor
<!-- Correct -->
<MudIconButton Icon="@Icons.Material.Filled.Add" />
<MudButton StartIcon="@Icons.Material.Filled.Save">Save</MudButton>

<!-- Wrong - Icon will not display -->
<MudIconButton Icon="Icons.Material.Filled.Add" />
```

### 3. Generic Component Type Parameters

Explicitly specify type parameter `T` for generic MudBlazor components:

```razor
<!-- Correct -->
<MudSwitch T="bool" @bind-Value="@isEnabled" />
<MudChip T="string" Value="@chipValue" />
<MudTextField T="string" @bind-Value="@textValue" />
<MudSelect T="int" @bind-Value="@selectedId">
    <MudSelectItem T="int" Value="1">Option 1</MudSelectItem>
</MudSelect>

<!-- Wrong - May cause type inference errors -->
<MudSwitch @bind-Value="@isEnabled" />
```

### 4. Component Lifecycle

**Never perform JavaScript interop or time-consuming operations in `OnInitializedAsync`.**

During static rendering, JS interop calls can only execute in `OnAfterRenderAsync`. Place initialization logic there:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // JavaScript interop
        await JSRuntime.InvokeVoidAsync("initializeChart");

        // Time-consuming data loading
        await LoadDataAsync();

        StateHasChanged();
    }
}
```

Use `CancellationToken` for async operations:

```csharp
@implements IAsyncDisposable

@code {
    private CancellationTokenSource? _cts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _cts = new CancellationTokenSource();
            await LoadDataAsync(_cts.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

### 5. Theme and Color Usage

**Always use MudBlazor CSS variables for colors.** Never hardcode color values.

```css
/* Correct */
.my-component {
    background-color: var(--mud-palette-surface);
    color: var(--mud-palette-text-primary);
    border-color: var(--mud-palette-lines-default);
}

/* Wrong */
.my-component {
    background-color: #ffffff;
    color: #424242;
}
```

**Support both light and dark modes.** All custom colors must consider theme switching.

Common palette variables:
- `--mud-palette-primary` / `--mud-palette-primary-text`
- `--mud-palette-surface` / `--mud-palette-background`
- `--mud-palette-text-primary` / `--mud-palette-text-secondary`
- `--mud-palette-lines-default` / `--mud-palette-lines-inputs`
- `--mud-palette-action-default` / `--mud-palette-action-default-hover`

For complete CSS variable reference, see `references/mudblazor-css-variables.md`.

### 6. MudBlazor 9.0.0 API Requirements

**All synchronous methods have been removed.** Always use async methods:

```csharp
// Correct (v9)
var dialog = await DialogService.ShowAsync<MyDialog>();
await DialogService.ShowMessageBoxAsync();
await dataGrid.ExpandAllGroupsAsync();
await select.ClearAsync();
await tabs.ActivatePanelAsync();

// Wrong (removed in v9)
var dialog = DialogService.Show<MyDialog>();  // Removed
DialogService.ShowMessageBox();  // Removed
dataGrid.ExpandAllGroups();  // Removed
```

**Converters have been completely redesigned.** Custom converters must implement new interfaces:

```csharp
// Correct (v9) - Implement IReversibleConverter
public class MyConverter : IReversibleConverter<MyType, string>
{
    public string Convert(MyType input) => input?.ToString() ?? string.Empty;
    public MyType ConvertBack(string input) => MyType.Parse(input);
}

// For inline converters, use Conversions.From()
private IConverter<MyType?, string?> _converter = Conversions
    .From((MyType? value) => value?.ToString(),
          text => new MyType { Name = text });
```

**Custom form components must implement GetDefaultConverter():**

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

**MudGlobal theming properties removed.** Use CSS variables, theme configuration, or explicit component parameters instead:

```csharp
// Wrong (removed in v9)
MudGlobal.ButtonDefaults.Color = Color.Primary;
MudGlobal.InputDefaults.Variant = Variant.Outlined;

// Correct (v9) - Use component parameters
<MudButton Color="Color.Primary">Button</MudButton>
<MudTextField Variant="Variant.Outlined" />
```

For complete migration guide, see `references/migration-guide-v9.md` (for v8 to v9 migration, see `references/migration-guide-v8.md`).

### 7. Offline/Intranet Requirements

All UI modules must support offline environments:

- **No online font CDN**: Never reference Google Fonts, Adobe Fonts, etc.
- **Local fonts**: Store all fonts in `wwwroot/fonts/`
- **No external CDN**: All static resources must be local
- **Intranet compatibility**: Consider environments without internet access

For font management workflow, see `references/offline-requirements.md`.

### 8. Browser Storage (`IMoBrowserStorage`)

**Always use `IMoBrowserStorage` for browser storage** — never use raw `IJSRuntime` calls for localStorage/sessionStorage.

**Key naming convention**: `{category}:{id}` (auto-prefixed with `mo:` by the service).

**Load persisted state in `OnAfterRenderAsync`**, use deferred rendering to prevent flash of default values:

```csharp
@inject IMoBrowserStorage BrowserStorage

@code {
    private bool _stateLoaded = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var state = await BrowserStorage.GetTableStateAsync("my-table");
            // Apply state...
            _stateLoaded = true;
            await InvokeAsync(StateHasChanged);
        }
    }
}
```

**Use `BrowserStorageExtensions`** for table state persistence (`GetTableStateAsync`/`SaveTableStateAsync`).

For complete API reference, patterns, and examples, see `references/browser-storage-guide.md`.

For detailed component architecture patterns (hierarchy, communication, state management), see `references/blazor-best-practices.md`.

### 9. Localization (i18n)

**Always use localization for user-facing text** — never hardcode text strings in components.

#### Decentralized Pattern

Each UI module should manage its own localization resources independently. This improves modularity and reduces coupling.

**1. Create marker class** in your UI module:

```csharp
// Monica.StateStore.UI/Localization/StateStoreResource.cs
namespace Monica.StateStore.UI.Localization;

/// <summary>
/// Marker class for StateStore UI localization resources
/// </summary>
public class StateStoreResource
{
}
```

**2. Add JSON files** in `Localization/{ResourceName}/` folder:
- `Localization/StateStoreResource/zh-CN.json`
- `Localization/StateStoreResource/en-US.json`

**3. Configure csproj** to embed JSON files:

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization\**\*.json" />
</ItemGroup>
```

**4. Inject in components:**

```razor
@using Microsoft.Extensions.Localization
@using Monica.StateStore.UI.Localization
@inject IStringLocalizer<StateStoreResource> L

<MudButton>@L["Dashboard:Title"]</MudButton>
<MudText>@L["KeyExplorer:Actions:Scan"]</MudText>
```

**Key naming**: Use hierarchical paths without module prefix (`Dashboard:Title`, not `StateStore:Dashboard:Title`).

**Auto-discovery**: `MoStringLocalizerFactory` automatically discovers resources from all assemblies starting with "Monica" in namespaces containing ".Localization".

#### General Requirements

**Both languages required:** When adding new keys, update both `zh-CN.json` and `en-US.json`.

**Remove unused keys:** Localization files should only contain keys that are actively used in the code.

**Validation workflow:**
```bash
# Run before commit to check for missing/unused keys
python .claude/skills/mo-ui-development/scripts/validate_localization.py

# If unused keys are found, remove them from both language files
```

For complete patterns, auto-discovery mechanism, JSON structure, parameterized strings, and migration guide, see `references/localization-guide.md`.

## Service Error Handling in Components

For service layer patterns including `Res<T>` return values and the `IsFailed` handling pattern, see the **mo-development** skill.

When calling services from Blazor components, handle errors with user feedback via Snackbar:

```csharp
private async Task LoadDataAsync()
{
    if ((await UserService.GetDataAsync(id)).IsFailed(out var error, out var data))
    {
        Snackbar.Add($"Error: {error.Message}", Severity.Error);
        return;
    }

    // Process data
    ProcessData(data);
}
```

For detailed performance optimization and error boundary patterns, see `references/blazor-best-practices.md`.

## UI Module Development

For UI module architecture patterns (Mixed, Standalone, Framework), module class implementation, folder conventions, and component organization, see `references/module-structure-guide.md`.

## Additional Resources

### Reference Files

For comprehensive guidance, consult these reference files:

- **`references/module-structure-guide.md`** - UI folder conventions, naming patterns, component organization
- **`references/blazor-best-practices.md`** - Component architecture, lifecycle, state management, form handling, accessibility patterns
- **`references/theme-css-guide.md`** - Theme architecture, CSS variable naming, special effects (glassmorphic, gradients), responsive design
- **`references/mudblazor-css-variables.md`** - Complete palette properties, shadows, layout properties, typography CSS variables
- **`references/component-reference.md`** - Component categories, common code examples, component properties
- **`references/migration-guide-v9.md`** - Complete v9.0.0 breaking changes and migration patterns (v8 → v9)
- **`references/migration-guide-v8.md`** - Complete v8.9.0 breaking changes and migration patterns (v7 → v8)
- **`references/css-isolation-fix-workflow.md`** - Step-by-step workflow for fixing CSS isolation issues
- **`references/offline-requirements.md`** - Font management and offline environment requirements
- **`references/browser-storage-guide.md`** - `IMoBrowserStorage` API, table state persistence, theme persistence, custom state patterns
- **`references/localization-guide.md`** - Localization patterns, key naming conventions, parameterized strings, validation workflow, migration guide

### Scripts

Utility scripts for common operations:

- **`scripts/font_downloader.py`** - Download Google Fonts for offline use. Supports single URL download, batch download (`--download-all`), weight filtering (`--weights`), and custom output directory. See `references/offline-requirements.md` for detailed usage.
- **`scripts/validate_localization.py`** - Validate localization keys for missing, unused, and language sync issues. Run before committing changes to ensure localization integrity.

### Quick Search Patterns

Find component implementations:
```bash
Glob: "**/Mud{ComponentName}.razor"
Grep: "<Mud{ComponentName}"
```

Find component styles:
```bash
Glob: "**/_mud{componentname}.scss"
```

## Quick Reference

### MudBlazor Version
Current project uses **MudBlazor 9.0.0**.

### Essential Checklist

- [ ] Use CSS isolation with `.razor.css` files (no `<style>` tags)
- [ ] Wrap MudBlazor components with div + `::deep` for styling
- [ ] Use `@` prefix for Icon properties
- [ ] Specify `T` parameter for generic components
- [ ] Place JS interop in `OnAfterRenderAsync`
- [ ] Use MudBlazor CSS variables for colors
- [ ] Support both light and dark modes
- [ ] Use async methods (all sync methods removed in v9)
- [ ] Implement `GetDefaultConverter()` for custom form components
- [ ] Use new converter interfaces (`IReversibleConverter`, `IConverter`)
- [ ] Avoid removed `MudGlobal` theming properties
- [ ] Ensure offline/intranet compatibility
- [ ] Use `IMoBrowserStorage` for browser persistence (never raw JS interop)
- [ ] Use localization for all user-facing text (never hardcode strings)
