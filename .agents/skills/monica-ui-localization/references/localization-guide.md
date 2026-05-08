# Localization Guide (i18n)

Comprehensive guidance for implementing multilingual support in Monica UI components.

## Architecture Overview

Monica uses a decentralized localization pattern where each UI module manages its own localization resources independently. This improves modularity, reduces coupling, and makes modules truly self-contained.

## Decentralized Pattern

### Overview

Each `*.UI` project has its own localization infrastructure:
- Marker class in `Localization/{ModuleName}Resource.cs`
- JSON files in `Localization/{ModuleName}Resource/zh-CN.json` and `en-US.json`
- Embedded resources configured in `.csproj`
- Auto-discovered by `MoStringLocalizerFactory`

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
  "KeyExplorer": {
    "Messages": {
      "FoundKeys": "Found {0} keys",
      "KeyRange": "Showing keys {0} to {1} of {2}"
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
```

**en-US.json:**
```json
{
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

## Validation Workflow

### Running the Script

**Basic validation:**
```bash
python scripts/validate_localization.py
```

**Summary only:**
```bash
python scripts/validate_localization.py --summary
```

**Strict mode** (treat unused keys as errors):
```bash
python scripts/validate_localization.py --strict
```

**JSON output** (for CI/CD):
```bash
python scripts/validate_localization.py --json
```

### Validation Checks

1. **Missing keys** (ERROR): Keys used in Razor or C# but not defined in JSON
2. **Unused keys** (WARNING): Keys defined in JSON but never used
3. **Language sync** (ERROR): Keys in one language but not another
4. **UI registry keys** (ERROR): Keys passed to `RegisterLocalizedComponent` must exist in `Monica.UI/Localization/UIRegistryResource/*.json`

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
    python <path-to-monica-ui-localization-skill>/scripts/validate_localization.py --strict --json
```

## Troubleshooting

### Key Not Found at Runtime

**Symptom:** Text displays as key path instead of translated text

**Solution:**
1. Verify key exists in JSON file
2. Check key path matches JSON structure exactly
3. Ensure JSON file is valid (no syntax errors)
4. Rebuild project

### Navigation/AppBar Key Shows Raw Text

**Symptom:** The navigation or AppBar shows a raw key such as `Pages:GitRepositories:Title`

**Cause:** `RegisterLocalizedComponent` keys are resolved from `UIRegistryResource`, not the page module resource.

**Solution:**
1. Keep page-local text in the module resource JSON files
2. Add the navigation/AppBar key to `Monica.UI/Localization/UIRegistryResource/zh-CN.json`
3. Add the same key to `Monica.UI/Localization/UIRegistryResource/en-US.json`
4. Re-run `python scripts/validate_localization.py`

### Parameterized String Shows {0}

**Symptom:** Parameters show as `{0}`, `{1}` instead of values

**Solution:**
```razor
<!-- Wrong -->
@L["KeyExplorer:Messages:FoundKeys"]

<!-- Correct -->
@L["KeyExplorer:Messages:FoundKeys", count]
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
2. **Inject once** - Add `@inject IStringLocalizer<{YourModule}Resource> L` at component top
3. **Descriptive keys** - Use `KeyExplorer:Actions:Scan` not `KE:AS`
4. **Keep languages in sync** - Always update both JSON files
5. **Validate before commit** - Run validation script before committing
6. **Test both languages** - Verify text displays correctly in all languages
7. **Use parameters** - For dynamic text, use parameterized strings
8. **Follow naming conventions** - PascalCase and hierarchical structure
