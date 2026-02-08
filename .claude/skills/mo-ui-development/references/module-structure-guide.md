# UI Module Structure Guide

This guide defines the architecture patterns, folder conventions, and component organization for UI modules in Monica.

> **Infrastructure module patterns** (module classes, options, guides, builder extensions, service layer patterns) are documented in the **mo-development** skill's `references/module-patterns.md`.

## UI Module Architecture Patterns

Monica has three distinct patterns for UI modules. Choose based on the relationship between infrastructure and UI code.

### Pattern A: Mixed Module

**When to use**: Infrastructure and UI are tightly coupled and maintained together.

A single project contains both infrastructure and UI code, with two separate module classes.

**Example**: `Monica.DataChannel`

```
Monica.DataChannel/
├── Modules/
│   ├── ModuleDataChannel.cs           # Infrastructure module
│   └── ModuleDataChannelUI.cs         # UI module
├── Pages/
│   └── UIDataChannelPage.razor        # Page component
├── UIDataChannel/                      # UI-specific folder
│   ├── Components/
│   ├── Models/
│   └── Services/
│       └── DataChannelUIService.cs    # UI service
├── Services/                           # Infrastructure services
└── [other infrastructure code]
```

Key characteristics:
- Two module classes: `ModuleDataChannel` (infrastructure) + `ModuleDataChannelUI` (UI)
- UI code lives in `UI{Name}/` subfolder to separate from infrastructure
- UI service named `{Name}UIService` (e.g., `DataChannelUIService`)
- Pages at project root `Pages/` folder

### Pattern B: Standalone UI Module

**When to use**: UI is complex enough to warrant its own project, or the infrastructure module is in a separate package.

A dedicated `Monica.{Name}.UI` project that references the infrastructure module.

**Example**: `Monica.Configuration.UI`

```
Monica.Configuration.UI/
├── Modules/
│   └── ModuleConfigurationUI.cs       # UI module
├── Pages/
│   └── UIConfigurationDashboardPage.razor
├── Services/
│   └── ConfigurationUIService.cs      # UI service at project root
├── Components/                         # Components at project root
├── Models/
└── Interfaces/
```

Key characteristics:
- Single UI module class: `ModuleConfigurationUI`
- **Flat structure**: `Components/`, `Services/`, `Models/` at project root (since there's only one UI module per project)
- UI service named `{Name}UIService` (e.g., `ConfigurationUIService`)
- References infrastructure module via project/package dependency

### Pattern C: Framework UI Module

**When to use**: Aggregating multiple small UI modules that don't warrant their own projects.

A container project hosting multiple UI sub-modules, each with its own `UI{Name}/` folder.

**Example**: `Monica.Framework.UI`

```
Monica.Framework.UI/
├── Modules/
│   ├── ModuleFrameworkUI.cs           # Parent/aggregator module
│   ├── ModuleLoggingUI.cs            # Sub-module
│   └── ModuleEventBusUI.cs           # Sub-module
├── Pages/
│   ├── UILoggingPage.razor
│   └── UIEventBusPage.razor
├── UILogging/                          # Per-feature UI folder
│   ├── Components/
│   ├── Models/
│   └── Services/
│       └── LoggingUIService.cs
└── UIEventBus/
    ├── Components/
    └── Services/
        └── EventBusUIService.cs
```

Key characteristics:
- One parent module + N sub-modules, each with own `UI{Name}/` folder
- Each sub-module has its own `UI{Name}/` folder to separate from other modules
- UI services named `{Name}UIService` (e.g., `LoggingUIService`)

### Pattern Selection Guide

| Criteria | Mixed (A) | Standalone (B) | Framework (C) |
|----------|-----------|----------------|---------------|
| Infrastructure + UI coupling | Tight | Loose | N/A |
| UI complexity | Any | High | Low per module |
| Number of UI modules | 1 | 1 | Multiple |
| Separate deployment | No | Yes | No |

## UI Module Class Implementation

```csharp
public class Module{Name}UI(Module{Name}UIOption option)
    : MoModuleWithDependencies<Module{Name}UI, Module{Name}UIOption, Module{Name}UIGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.{Name}UI;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<{Name}UIService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.Disable{Name}Page)
        {
            DependsOnModule<Module{Name}Guide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UI{Name}Page>(
                    UI{Name}Page.{NAME}_URL,
                    "{Name} Dashboard",
                    Icons.Material.Filled.Settings,
                    "System Management",
                    addToNav: true,
                    navOrder: 100));
        }
    }
}
```

## Naming Conventions

When creating UI modules, use these naming patterns:

| Variable | Format | Example |
|----------|--------|---------|
| `$ModuleName$` | PascalCase | `SignalR`, `SystemInfo` |
| `$ModuleUIName$` | `{ModuleName}UI` | `SignalrUI`, `SystemInfoUI` |
| `$UIFolderName$` | `UI{ModuleName}` | `UISignalr`, `UISystemInfo` |
| `$PageName$` | `UI{ModuleName}Page` | `UISignalRPage`, `UISystemInfoPage` |
| `$RouteURL$` | kebab-case | `/{module-name}-debug`, `/{module-name}-manage` |
| `$ServiceName$` | `{ModuleName}UIService` | `DataChannelUIService`, `ConfigurationUIService` |

## UI Folder Structure

### Mixed / Framework UI Projects

Use `UI{ModuleName}/` subfolders to separate UI code from infrastructure or other modules:

```
UI{ModuleName}/
├── Components/     # Blazor components specific to this module
├── Models/         # View models (only create when necessary - reuse source module models)
└── Services/       # UI services ({ModuleName}UIService)
```

### Standalone UI Projects

Use flat structure at project root (since there's only one UI module per project):

```
Monica.{Name}.UI/
├── Modules/
│   └── Module{Name}UI.cs
├── Pages/
│   └── UI{Name}Page.razor
├── Components/         # At project root
├── Services/           # At project root
│   └── {Name}UIService.cs
├── Models/
└── Interfaces/
```

## Page File

- **Location**: `Pages/UI{ModuleName}Page.razor`
- **Route definition**:

```csharp
@attribute [Route({NAME}_URL)]

@code {
    public const string {NAME}_URL = "/module-name-page";
}
```

### Page Dependency Injection

```csharp
@using Monica.Framework.UI.{UIFolderName}.Components
@using Monica.Framework.UI.{UIFolderName}.Services
@using Monica.Framework.UI.{UIFolderName}.Models
@inject {ModuleName}UIService {ModuleName}UIService
```

## Data Model Naming Conventions

- **Location**: `UI{ModuleName}/Models/` (or `Models/` for standalone projects)
- **Use strongly-typed models**, avoid dynamic types
- **Prioritize reusing source module models** over creating new ones
- **Composition over redefinition**: When new models are needed, compose from source module models

| Type | Naming Pattern |
|------|---------------|
| Request models | `{Feature}Request` |
| Response models | `{Feature}Response` |
| View models | `{Feature}ViewModel` |
| DTOs | `Dto{Feature}` |

## Example References

### UISignalR Module (Framework UI)

```
Modules/SignalrUI.cs                           # Module class
UISignalr/                                     # UI folder
├── Components/SignalRConnectionConfig.razor   # Components
├── Components/SignalRMessageLog.razor
└── Services/SignalRUIService.cs               # UI service
Pages/UISignalRPage.razor                      # Page
```

### UIDataChannel Module (Mixed)

```
Modules/ModuleDataChannelUI.cs                 # UI module class
UIDataChannel/                                 # UI folder
├── Components/ChannelStatusCard.razor
├── Models/ChannelStatusInfo.cs
└── Services/DataChannelUIService.cs           # UI service
Pages/UIDataChannelPage.razor                  # Page
```

### Configuration UI Module (Standalone)

```
Monica.Configuration.UI/
├── Modules/ModuleConfigurationUI.cs           # UI module class
├── Pages/UIConfigurationDashboardPage.razor
├── Services/ConfigurationUIService.cs         # UI service at root
├── Components/                                # Components at root
└── Models/
```
