# Localization Guide (i18n)

Comprehensive guidance for implementing multilingual support in Monica UI components.

## Architecture Overview

Monica supports two localization patterns:

1. **Decentralized Pattern (Recommended)** - Each UI module manages its own localization resources independently
2. **Centralized Pattern (Legacy)** - All modules share `Monica.UI/Localization/SharedResource`

**For new UI modules, always use the decentralized pattern.** It improves modularity, reduces coupling, and makes modules truly self-contained.

## Decentralized Pattern (Recommended)

### Overview

Each `*.UI` project has its own localization infrastructure:
- Marker class in `Localization/{ModuleName}Resource.cs`
- JSON files in `Localization/{ModuleName}Resource/zh-CN.json` and `en-US.json`
- Embedded resources configured in `.csproj`
- Auto-discovered by `MoStringLocalizerFactory`

**Benefits:**
- True module independence
- No coupling to Monica.UI
- Simpler key names (no module prefix needed)
- Easier to maintain and version independently

### Auto-Discovery Mechanism

`MoStringLocalizerFactory` automatically discovers localization resources:

1. Scans all assemblies starting with "Monica"
2. Finds types in namespaces containing ".Localization"
3. For each resource type, loads embedded JSON files using pattern: `{Namespace}.{ResourceName}.{Culture}.json`

**Example:** For type `StateStoreResource` in namespace `Monica.StateStore.UI.Localization`, it loads:
- `Monica.StateStore.UI.Localization.StateStoreResource.zh-CN.json`
- `Monica.StateStore.UI.Localization.StateStoreResource.en-US.json`

**No registration code needed!** Just create the marker class, add JSON files, and configure embedding.

### Implementation Steps

#### Step 1: Create Marker Class

Create a marker class in your UI module:

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

**Naming convention:** `{ModuleName}Resource` (e.g., `StateStoreResource`, `ConfigurationResource`)

#### Step 2: Create JSON Files

Create JSON files in `Localization/{ResourceName}/` folder:

```
Monica.StateStore.UI/
└── Localization/
    └── StateStoreResource/
        ├── zh-CN.json
        └── en-US.json
```

**JSON structure:**

```json
{
  "texts": {
    "Dashboard": {
      "PageTitle": "State Store Management",
      "Title": "State Store Dashboard",
      "Tabs": {
        "ProviderOverview": "Provider Overview",
        "KeyExplorer": "Key Explorer"
      }
    },
    "KeyExplorer": {
      "Labels": {
        "KeyPattern": "Key Pattern",
        "KeyName": "Key Name"
      },
      "Actions": {
        "Scan": "Scan Keys",
        "Refresh": "Refresh"
      }
    }
  }
}
```

**Key naming:** Use hierarchical structure without module prefix:
- ✅ `"Dashboard:PageTitle"` (correct)
- ❌ `"StateStore:Dashboard:PageTitle"` (wrong - no module prefix needed)

#### Step 3: Configure Project File

Update your `.csproj` to embed JSON files:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <!-- ... other configuration ... -->

  <ItemGroup>
    <EmbeddedResource Include="Localization\**\*.json" />
  </ItemGroup>
</Project>
```

This embeds all JSON files in the `Localization` folder as embedded resources.

#### Step 4: Update _Imports.razor

Add the localization namespace to your module's `_Imports.razor`:

```razor
@using Microsoft.Extensions.Localization
@using Monica.StateStore.UI.Localization
```

#### Step 5: Use in Components

Inject the localizer in your components:

```razor
@inject IStringLocalizer<StateStoreResource> L

<MudText Typo="Typo.h4">@L["Dashboard:PageTitle"]</MudText>
<MudButton OnClick="ScanKeys">@L["KeyExplorer:Actions:Scan"]</MudButton>
<MudTextField Label="@L["KeyExplorer:Labels:KeyPattern"]" />
```

### Key Naming Conventions

**Hierarchical structure** matching JSON organization:

```
ComponentName (top-level)
├── Labels (UI labels)
├── Placeholders (input placeholders)
├── Actions (button text)
├── Messages (user feedback)
├── Dialogs (dialog titles/content)
└── Tabs (tab labels)
```

**Examples:**

```razor
@L["Dashboard:PageTitle"]
@L["KeyExplorer:Labels:KeyPattern"]
@L["KeyExplorer:Actions:Scan"]
@L["KeyExplorer:Messages:FoundKeys", count]
@L["KeyEditor:Dialogs:ConfirmDelete"]
```

**Rules:**
- PascalCase for all parts
- No module prefix (e.g., no "StateStore:" prefix)
- Descriptive names (e.g., `InitializationComplete` not `InitDone`)
- Consistent terminology across the module

### Parameterized Strings

**JSON:**
```json
{
  "texts": {
    "KeyExplorer": {
      "Messages": {
        "FoundKeys": "Found {0} keys",
        "KeyRange": "Showing keys {0} to {1} of {2}"
      }
    }
  }
}
```

**Razor:**
```razor
@L["KeyExplorer:Messages:FoundKeys", count]
@L["KeyExplorer:Messages:KeyRange", start, end, total]
```

### Complete Example

**File structure:**
```
Monica.StateStore.UI/
├── Localization/
│   ├── StateStoreResource.cs
│   └── StateStoreResource/
│       ├── zh-CN.json
│       └── en-US.json
├── _Imports.razor
├── Monica.StateStore.UI.csproj
└── UIStateStore/
    └── Components/
        └── StateStoreKeyExplorer.razor
```

**StateStoreResource.cs:**
```csharp
namespace Monica.StateStore.UI.Localization;

public class StateStoreResource { }
```

**zh-CN.json:**
```json
{
  "texts": {
    "KeyExplorer": {
      "Labels": {
        "KeyPattern": "键模式"
      },
      "Actions": {
        "Scan": "扫描键"
      },
      "Messages": {
        "FoundKeys": "找到 {0} 个键"
      }
    }
  }
}
```

**en-US.json:**
```json
{
  "texts": {
    "KeyExplorer": {
      "Labels": {
        "KeyPattern": "Key Pattern"
      },
      "Actions": {
        "Scan": "Scan Keys"
      },
      "Messages": {
        "FoundKeys": "Found {0} keys"
      }
    }
  }
}
```

**_Imports.razor:**
```razor
@using Microsoft.Extensions.Localization
@using Monica.StateStore.UI.Localization
```

**StateStoreKeyExplorer.razor:**
```razor
@inject IStringLocalizer<StateStoreResource> L

<MudTextField Label="@L["KeyExplorer:Labels:KeyPattern"]" />
<MudButton OnClick="ScanKeys">@L["KeyExplorer:Actions:Scan"]</MudButton>

@code {
    private async Task ScanKeys()
    {
        var keys = await ScanAsync();
        Snackbar.Add(L["KeyExplorer:Messages:FoundKeys", keys.Count], Severity.Success);
    }
}
```

### Migration from SharedResource

If you have an existing module using SharedResource, follow these steps:

**1. Create new localization infrastructure:**
- Create marker class
- Create JSON files
- Configure csproj

**2. Extract keys from SharedResource:**
- Copy your module's section from `Monica.UI/Localization/SharedResource/zh-CN.json`
- Remove module prefix from keys (e.g., `"StateStore:Dashboard:Title"` → `"Dashboard:Title"`)
- Paste into your new JSON files

**3. Update components:**
- Change injection: `IStringLocalizer<SharedResource>` → `IStringLocalizer<{YourModule}Resource>`
- Update key references: remove module prefix from all keys
- Update `_Imports.razor` to use new namespace

**4. Remove from SharedResource:**
- Delete your module's section from both `zh-CN.json` and `en-US.json` in Monica.UI

**5. Validate:**
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py
```

## Centralized Pattern (Legacy)

### Quick Start

**Supported Languages:** zh-CN (default), en-US
**Location:** `Monica.UI/Localization/SharedResource/`
**Shared across:** All `*.UI` projects
**When to use:** Only for existing modules already using SharedResource

### Basic Usage

```razor
@using Microsoft.Extensions.Localization
@using Monica.UI.Localization
@inject IStringLocalizer<SharedResource> L

<MudButton>@L["Common:Save"]</MudButton>
<MudText>@L["ModuleSystem:Dashboard:Title"]</MudText>
```

## Architecture

### File Structure

```
Monica.UI/Localization/SharedResource/
├── zh-CN.json          # Chinese translations
├── en-US.json          # English translations
└── SharedResource.cs   # Marker class
```

### JSON Structure

Hierarchical structure with `texts` root:

```json
{
  "texts": {
    "Common": {
      "Save": "保存",
      "Cancel": "取消"
    },
    "Layout": {
      "UserMenu": {
        "Profile": "个人资料"
      }
    }
  }
}
```

**Mapping:** `texts.Layout.UserMenu.Profile` → `@L["Layout:UserMenu:Profile"]`

## Key Naming Conventions

### Hierarchical Structure

Use colon-separated paths matching JSON hierarchy:

```razor
@L["Category:SubCategory:Key"]
@L["Common:Save"]
@L["Layout:UserMenu:Profile"]
@L["ModuleSystem:Dashboard:Title"]
```

### Standard Categories

**Common** - Shared UI elements
- `Common:Save`, `Common:Cancel`, `Common:Delete`
- `Common:Search`, `Common:Filter`, `Common:Refresh`

**Validation** - Form validation messages
- `Validation:Required`, `Validation:InvalidEmail`
- `Validation:MinLength`, `Validation:MaxLength`

**Messages** - User feedback
- `Messages:SaveSuccess`, `Messages:SaveFailed`
- `Messages:DeleteConfirm`, `Messages:LoadingData`

**Layout** - Layout components
- `Layout:SearchTooltip`, `Layout:NotificationsTooltip`
- `Layout:UserMenu:Profile`, `Layout:UserMenu:Settings`

**Feature-Specific** - Module/feature keys
- `ModuleSystem:Dashboard:Title`
- `UserManagement:AddUser`

### Naming Rules

1. **PascalCase**: `UserMenu`, `SystemStatus`, `SaveSuccess`
2. **Descriptive**: `InitializationComplete` not `InitDone`
3. **Consistent**: Always `Delete` (not `Remove` sometimes)
4. **No abbreviations**: `Description` not `Desc`

## Usage Patterns

### Parameterized Strings

**JSON:**
```json
{
  "texts": {
    "Validation": {
      "MinLength": "最少需要 {0} 个字符",
      "Range": "值必须在 {0} 到 {1} 之间"
    }
  }
}
```

**Razor:**
```razor
@L["Validation:MinLength", 5]
<!-- Output: "最少需要 5 个字符" -->

@L["Validation:Range", 1, 100]
<!-- Output: "值必须在 1 到 100 之间" -->
```

### Component Properties

```razor
<!-- Tooltips -->
<MudIconButton Icon="@Icons.Material.Filled.Search"
               Title="@L["Layout:SearchTooltip"]" />

<!-- Labels -->
<MudTextField Label="@L["Common:Name"]" @bind-Value="@name" />

<!-- Menu Items -->
<MudMenuItem Icon="@Icons.Material.Filled.Person">
    @L["Layout:UserMenu:Profile"]
</MudMenuItem>
```

### Conditional Rendering

```razor
@if (isInitialized)
{
    <MudText>@L["ModuleSystem:SystemOverview:InitializationComplete"]</MudText>
}
else
{
    <MudText>@L["ModuleSystem:SystemOverview:Initializing"]</MudText>
}
```

## Adding New Keys

### Step-by-Step

**1. Choose category and key name**
```
Category: ModuleSystem:ModuleManagement
Key: RefreshData
```

**2. Add to zh-CN.json**
```json
{
  "texts": {
    "ModuleSystem": {
      "ModuleManagement": {
        "RefreshData": "刷新数据"
      }
    }
  }
}
```

**3. Add to en-US.json**
```json
{
  "texts": {
    "ModuleSystem": {
      "ModuleManagement": {
        "RefreshData": "Refresh Data"
      }
    }
  }
}
```

**4. Use in component**
```razor
@inject IStringLocalizer<SharedResource> L

<MudButton OnClick="RefreshData">
    @L["ModuleSystem:ModuleManagement:RefreshData"]
</MudButton>
```

**5. Validate**
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py
```

## Common Patterns

### Pattern 1: Form Validation

```razor
<MudTextField Label="@L["Common:Name"]"
              @bind-Value="@model.Name"
              Required="true"
              RequiredError="@L["Validation:Required"]" />

<MudTextField Label="@L["Common:Email"]"
              @bind-Value="@model.Email"
              Validation="@(new EmailAddressAttribute() { 
                  ErrorMessage = L["Validation:InvalidEmail"] 
              })" />
```

### Pattern 2: Snackbar Messages

```razor
@inject ISnackbar Snackbar

@code {
    private async Task SaveData()
    {
        try
        {
            await DataService.SaveAsync(data);
            Snackbar.Add(L["Messages:SaveSuccess"], Severity.Success);
        }
        catch
        {
            Snackbar.Add(L["Messages:SaveFailed"], Severity.Error);
        }
    }
}
```

### Pattern 3: Dialog Confirmation

```razor
@inject IDialogService DialogService

@code {
    private async Task DeleteItem()
    {
        var result = await DialogService.ShowMessageBox(
            L["Common:Confirm"],
            L["Messages:DeleteConfirm"],
            yesText: L["Common:Yes"],
            cancelText: L["Common:No"]
        );

        if (result == true)
        {
            // Perform deletion
        }
    }
}
```

## Validation Workflow

### Running the Script

**Basic validation:**
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py
```

**Summary only:**
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py --summary
```

**Strict mode** (treat unused keys as errors):
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py --strict
```

**JSON output** (for CI/CD):
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py --json
```

### Validation Checks

1. **Missing keys** (ERROR): Keys used in Razor but not defined in JSON
2. **Unused keys** (WARNING): Keys defined in JSON but never used
3. **Language sync** (ERROR): Keys in one language but not another

### Example Output

```
=== Localization Validation Report ===

[ERROR] Missing Keys (used but not defined):
  ✗ UserManagement:Title
    Used in: Monica.UI/Components/Pages/UserManagement.razor:15

[WARNING] Unused Keys (defined but not used):
  ⚠ Common:Import
    Defined in: zh-CN.json, en-US.json

[ERROR] Language Sync Issues:
  ✗ ModuleSystem:NewFeature:Title
    Present in: zh-CN.json
    Missing in: en-US.json

Summary:
  Total keys: 245
  Missing: 1
  Unused: 1
  Sync issues: 1
  Status: FAILED
```

### Fixing Issues

**Missing keys:** Add to both JSON files  
**Unused keys:** Remove from both JSON files (verify not used dynamically)  
**Sync issues:** Add missing translations to ensure both files match

### CI/CD Integration

```yaml
- name: Validate Localization
  run: |
    python .claude/skills/mo-ui-development/scripts/validate_localization.py --strict --json
```

## Migration Guide

### Converting Hardcoded Text

**Before:**
```razor
<MudButton Color="Color.Primary">保存</MudButton>
<MudText Typo="Typo.h5">系统状态</MudText>
<MudIconButton Icon="@Icons.Material.Filled.Search"
               Title="搜索 (Ctrl+K)" />
```

**Step 1: Add keys to JSON**

zh-CN.json:
```json
{
  "texts": {
    "Common": { "Save": "保存" },
    "SystemOverview": { "SystemStatus": "系统状态" },
    "Layout": { "SearchTooltip": "搜索 (Ctrl+K)" }
  }
}
```

en-US.json:
```json
{
  "texts": {
    "Common": { "Save": "Save" },
    "SystemOverview": { "SystemStatus": "System Status" },
    "Layout": { "SearchTooltip": "Search (Ctrl+K)" }
  }
}
```

**Step 2: Update component**
```razor
@inject IStringLocalizer<SharedResource> L

<MudButton Color="Color.Primary">@L["Common:Save"]</MudButton>
<MudText Typo="Typo.h5">@L["SystemOverview:SystemStatus"]</MudText>
<MudIconButton Icon="@Icons.Material.Filled.Search"
               Title="@L["Layout:SearchTooltip"]" />
```

**Step 3: Test and validate**
1. Test in both languages
2. Run validation script
3. Commit changes

## Troubleshooting

### Key Not Found at Runtime

**Symptom:** Text displays as key path instead of translated text

**Solution:**
1. Verify key exists in JSON file
2. Check key path matches JSON structure exactly
3. Ensure JSON file is valid (no syntax errors)
4. Rebuild project

### Parameterized String Shows {0}

**Symptom:** Parameters show as `{0}`, `{1}` instead of values

**Solution:**
```razor
<!-- Wrong -->
@L["Validation:MinLength"]

<!-- Correct -->
@L["Validation:MinLength", 5]
```

### Language Sync Issues

**Symptom:** Validation script reports keys missing in one language

**Solution:**
1. Run validation to identify missing keys
2. Add missing keys to the other language file
3. Ensure both files have identical structure
4. Re-run validation

### JSON Syntax Error

**Symptom:** Localization not working at all

**Solution:**
1. Validate JSON files using a JSON validator
2. Check for:
   - Missing commas
   - Trailing commas
   - Unescaped quotes
   - Mismatched brackets

## Best Practices

1. **Always use localization** - Never hardcode user-facing text
2. **Inject once** - Add `@inject IStringLocalizer<SharedResource> L` at component top
3. **Descriptive keys** - Use `ModuleManagement:RefreshData` not `MM:RD`
4. **Keep languages in sync** - Always update both JSON files
5. **Validate before commit** - Run validation script before committing
6. **Test both languages** - Verify text displays correctly in all languages
7. **Use parameters** - For dynamic text, use parameterized strings
8. **Follow naming conventions** - PascalCase and hierarchical structure

## Quick Reference

**Injection:**
```razor
@inject IStringLocalizer<SharedResource> L
```

**Basic usage:**
```razor
@L["Category:Key"]
```

**With parameters:**
```razor
@L["Category:Key", param1, param2]
```

**Validation:**
```bash
python .claude/skills/mo-ui-development/scripts/validate_localization.py
```

**Files:**
- `Monica.UI/Localization/SharedResource/zh-CN.json`
- `Monica.UI/Localization/SharedResource/en-US.json`
- `.claude/skills/mo-ui-development/scripts/validate_localization.py`
