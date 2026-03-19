# Monica Module Patterns

This guide defines the standardized patterns and conventions for creating modules in Monica.

> **Canonical architecture reference**: `mo-architecture` skill (`SKILL.md`)
> When this file conflicts with the architecture skill, the architecture skill takes precedence.

## Module Naming Conventions

| Component | Naming Pattern | Example |
|-----------|---------------|---------|
| Module class | `Module{Name}` | `ModuleSignalR`, `ModuleJobScheduler` |
| Options class | `Module{Name}Option` | `ModuleSignalROption` |
| Guide class | `Module{Name}Guide` | `ModuleSignalRGuide` |
| Builder extensions | `extension(Mo)` with `Add{Name}()` | `Mo.AddSignalR()` |
| Module key entry | `EMoModuleKey.{Name}` | `EMoModuleKey.SignalR` |

## Standard Infrastructure Module Structure

```
Monica.{Name}/
├── Modules/
│   └── Module{Name}.cs
│
├── Abstractions/                    # Public contracts
│   ├── I{Feature}.cs
│   └── Internal/                    # Internal-only contracts
│       └── I{InternalContract}.cs
│
├── Models/                          # Public data contracts
│   ├── {Entity}.cs
│   └── Internal/                    # Internal-only models
│       └── {InternalModel}.cs
│
├── Facades/                         # Public entry points (Res<T>)
│   └── {Name}Facade.cs
│
├── Services/                        # Internal implementation
│   ├── {Feature}Service.cs
│   └── Support/
│       ├── {Feature}Registry.cs
│       ├── {Feature}Resolver.cs
│       └── {Feature}Coordinator.cs
│
├── Providers/                       # Pluggable implementations
│   └── {ProviderName}/
│       └── {Name}Provider.cs
│
├── Extensions/                      # (optional)
├── Events/                          # (optional)
├── Exceptions/                      # (optional)
└── Utils/                           # (optional, unified utility folder)
```

### Public vs Internal Boundary

- `Abstractions/` and `Models/` — public, consumed by external modules
- `Abstractions/Internal/` and `Models/Internal/` — private, module-internal only
- `Facades/` — public, consumed by Minimal API and UI
- `Services/` and `Providers/` — private, module-internal only

### Facade Pattern

Facades are the public entry point for API and UI consumers:

- Return `Res` / `Res<T>` exclusively
- Defined in the **infrastructure module** (not UI module)
- Shared by both Minimal API and UI — no separate UI service layer needed
- Delegate to `Services/` for implementation
- Must stay lightweight (~200 lines max per facade file)
- Other infrastructure modules do NOT consume Facades — they use `Abstractions/` interfaces

```csharp
public class {Name}Facade(
    ILogger<{Name}Facade> logger,
    {Feature}Service featureService,
    AnotherService anotherService)
{
    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        try
        {
            var result = await featureService.ProcessAsync(request);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operation failed");
            return Res.Fail($"Operation failed: {ex.Message}");
        }
    }
}
```

### Internal Service Pattern (Exceptions)

Services are internal implementation — use standard .NET patterns:

```csharp
internal class {Feature}Service(
    ILogger<{Feature}Service> logger,
    IOtherDependency dependency)
{
    public async Task<TResponse> ProcessAsync(TRequest request)
    {
        var result = await dependency.ExecuteAsync(request);
        return result ?? throw new KeyNotFoundException("Data not found");
    }
}
```

## Module Class Implementation

### Basic Module

```csharp
[ModuleKey(EMoModuleKey.{Name})]
public class Module{Name}(Module{Name}Option option)
    : MoModule<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register facade (public)
        services.AddScoped<{Name}Facade>();
        // Register services (internal)
        services.AddScoped<{Feature}Service>();
    }
}
```

### Module That Declares Dependencies

```csharp
[ModuleKey(EMoModuleKey.{Name})]
public class Module{Name}(Module{Name}Option option)
    : MoModule<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<{Name}Facade>();
        services.AddScoped<{Feature}Service>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleOtherGuide>().Register();
        DependsOnModule<ModuleAnotherGuide>().Register();
    }
}
```

## Options Class

```csharp
// Standard module options
public class Module{Name}Option : MoModuleOption<Module{Name}>
{
    public bool EnableFeature { get; set; } = true;
    public int MaxItems { get; set; } = 100;
}

// Module with Minimal API endpoints
public class Module{Name}Option : MoModuleOptionWithMinimalApi<Module{Name}>
{
    public bool EnableFeature { get; set; } = true;
}
```

## Guide Class (Fluent Configuration)

```csharp
public class Module{Name}Guide : MoModuleGuide<Module{Name}, Module{Name}Option, Module{Name}Guide>
{
    public Module{Name}Guide EnableFeature(bool enable = true)
    {
        Option.EnableFeature = enable;
        return this;
    }
}
```

## Builder Extensions

```csharp
public static Module{Name}Guide Add{Name}(Action<Module{Name}Option>? action = null)
{
    // Registration logic handled by Mo infrastructure
}
```

Usage:

```csharp
Mo.Add{Name}(options =>
{
    options.EnableFeature = true;
    options.MaxItems = 50;
});

Mo.Add{Name}()
    .EnableFeature()
    .WithMaxItems(50);
```

## Features Pattern (Bundled Sub-Modules)

When a module bundles multiple independent features:

```
Monica.{Name}/
├── Modules/
│   ├── Module{Name}.cs
│   └── Module{SubFeature}.cs
│
├── {FeatureA}/                      # Each feature follows the same layer convention
│   ├── Abstractions/
│   │   └── Internal/
│   ├── Models/
│   │   └── Internal/
│   ├── Facades/
│   ├── Services/
│   │   └── Support/
│   └── Providers/
│
├── {FeatureB}/
│   ├── Abstractions/
│   ├── Models/
│   ├── Facades/
│   └── Services/
│
└── Utils/                           # (optional, project-level)
```

Rules:
- Each feature folder follows the exact same layer convention as a top-level module
- Cross-feature shared types go in project-level folders
- Features must NOT depend on another feature's internal `Services/` — use `Abstractions/` for cross-feature contracts

## Using Options in Services

```csharp
public class {Name}Facade(
    IOptions<Module{Name}Option> options,
    ILogger<{Name}Facade> logger)
{
    private readonly Module{Name}Option _options = options.Value;

    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        if (!_options.EnableFeature)
        {
            return "Feature is disabled";
        }
        // ...
    }
}
```

## Minimal API Rules

Minimal API handlers must be thin — parameter extraction + facade call only:

```csharp
// Thin Minimal API — delegates to Facade
endpoints.MapPost("/api/{feature}",
    async ([FromRoute] string id,
          [FromServices] {Name}Facade facade,
          [FromBody] RequestModel request) =>
    {
        return await facade.ProcessAsync(id, request);
    });
```

If a Minimal API handler exceeds "parameter handling + 1-2 facade calls", the logic must move into the Facade.

## Dependency Direction

```
Minimal API / UI Component
    → Facade (Res<T>)
    → Services (internal, exceptions)
    → Providers (pluggable implementations)

Other Module
    → Abstractions (interfaces) + Models (public)
```

Forbidden:
- Service → Facade
- Provider → Facade
- Provider → UI
- Internal Service → another module's internal Service

## Best Practices

1. **Use primary constructors** for dependency injection
2. **Keep modules focused** — one module, one responsibility
3. **Declare dependencies explicitly** in `ClaimDependencies()`
4. **Use options for configuration** — inject `IOptions<TOption>`
5. **Follow naming conventions** — consistent naming makes code discoverable
6. **Facades return Res<T>** — internal services use standard returns + exceptions
7. **Use `Internal/` sub-folders** to separate public from private types
8. **Use `Utils/`** as the only utility folder name (not Helpers/Tools/Common)
