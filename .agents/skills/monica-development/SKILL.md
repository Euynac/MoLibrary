---
name: monica-development
description: This skill should be used when the user asks to "create module", "add module", "module structure", "use Res type", "return Res", "Res.Ok", "Res.Fail", "IsFailed pattern", "module registration", "module dependencies", "module pattern", "Monica architecture", "service layer pattern", "create service", "add service", "create hosted service", "add background service", "MoBackgroundService", "MoHostedService", "RecordState", "hosted service observability", "service state tracking", "CoordinatedLeaderService", "RequireFeature", "SatisfyFeature", "required config", "required configuration methods", "IUIModule", "IWebModule", "IWebHostRequiredModule", "web module", or needs guidance on Monica module architecture, the unified result model Res, module registration patterns, module runtime kinds, Res usage scope (UI vs infrastructure), required feature validation, or hosted service development with observability.
---

# Monica Development Guide

This skill provides essential guidance for developing modules and services in the Monica framework.

## Architecture Overview

Monica is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

### Module Pattern

Every module follows a consistent pattern with three components in one `Module{Name}.cs` file, which is located in the `Modules` folder of each project.

| Component | Purpose | Example |
|-----------|---------|---------|
| `Module{Name}` | Host-owned module strategy inheriting from `MonicaModule<TOptions>` | `ModuleSignalR` |
| `Module{Name}Option` | Configuration options for the module | `ModuleSignalROption` |
| `Module{Name}RegistrationExtensions` | Host registration and fluent feature extensions returning `ModuleRegistration<TModule, TOptions>` | `ModuleSignalRRegistrationExtensions` |

### Module Identity Rules

- The concrete module `Type` is the only graph and runtime identity. Do not add key attributes, key enums, or string identities to registration logic.
- `ModuleKey` is a diagnostic projection derived from the module type; never use it to resolve dependencies or determine graph uniqueness.
- Put first-party module strategies and `Add*` extensions in `Monica.Modules`. Use `$monica-third-party-module-development` for independently published packages.

### Module Runtime Kinds

Choose the module runtime kind before writing registration code:

- Use `MonicaModule<TOptions>` for every module strategy.
- Implement `IWebModule` only when the module actually configures ASP.NET Core middleware or endpoints.
- Implement `IUIModule` for every UI module. UI identity is explicit and independent of the type or project name.
- Do not equate `IUIModule` with `IWebModule`. A UI module that only registers pages, components, dialogs, or shell contributions has no web marker.
- Use `MinimalApiModuleOptions<TModule>` when the module exposes minimal APIs and needs API-group or API-disable controls.
- `IWebModule` is a capability marker: generic hosts omit its web contributions and retain its ordinary registrations.
- Add `IWebHostRequiredModule` only for an intrinsic type-level requirement. An opt-in web feature calls `registration.RequireWebHost(reason)` so the requirement appears only when that feature is selected.
- Choose middleware placement with `ModuleWebStage.BeforeRouting` or `ModuleWebStage.AfterRouting`; never use integer middleware priorities.
- Diagnostics keep web capability and the independent host requirement as separate facts.

### Localization Registration Rules

- If a module uses `IStringLocalizer<TResource>` directly or indirectly, some participating module in that dependency chain must declare:

```csharp
module.Require<ModuleLocalization, ModuleLocalizationOption>(options =>
    options.AddResource(typeof(TResource)));
```

- For Monica project-local resources, keep the marker class and JSON files under the project root `Localization/` folder so resource namespace, embedded resource path, and validation tooling stay aligned.
- Prefer constructor-injected `IStringLocalizer<TResource>` in modules' DI-created services, support classes, state classes, Razor components, pages, and dialogs.
- Monica localization must remain host-scoped. Never introduce ambient or static localization access. Static helpers and view models should accept an `IStringLocalizer` parameter or delegate display formatting to a cohesive formatter that receives one.
- When generic resource lookup is genuinely required, inject `ILocalizationCatalog`. At application-composition boundaries such as endpoint metadata configuration, resolve `IStringLocalizer<TResource>` or `ILocalizationCatalog` from the current host's service provider and keep the resolved service within that host.
- Preferred layout:

```text
Monica.{Project}/
└── Localization/
    ├── {Resource}.cs
    └── {Resource}/
        ├── zh-CN.json
        └── en-US.json
```

### Core Dependencies

**Monica.Core** is the foundation for all other modules, containing:
- `MonicaModule<TOptions>` module strategy
- Module registration system
- Automatic middleware ordering
- Core utilities and extensions

## Module Registration

Modules use a unified, host-bound registration pattern:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    // Basic registration with options
    monica.Add{ModuleName}(options =>
    {
        options.Property1 = value1;
        options.Property2 = value2;
    });

    // Feature extensions enrich the same host-bound registration.
    monica.Add{ModuleName}()
        .GuideMethod1()
        .GuideMethod2();
});
```

### Module Dependencies

When a module depends on other modules:

```csharp
public override void Describe(ModuleDescriptor module)
{
    module.Require<ModuleOther, ModuleOtherOption>();
    module.Require<ModuleAnother, ModuleAnotherOption>();
}
```

Modules declare dependencies in `Describe(ModuleDescriptor)` with `Require<TModule, TOptions>()` and optional ordering with `AfterIfPresent<TModule, TOptions>()`.

Dependencies are automatically registered when a module is added.

### Module State Ownership Rules

- Keep `Module{Name}Option` focused on developer configuration. Do not use options objects as mutable runtime registries for discovered types, generated endpoints, caches, or other cross-phase state.
- Do not put required shared runtime state in builder-extension local variables or closures inside `monica.Add{ModuleName}(...)`. A module can be included directly or transitively through `Require<TModule, TOptions>()`, and both paths must behave identically.
- If a module needs mutable state across registration phases such as `ConfigureServices`, `DeclareTypeDiscovery`, `PostConfigureServices`, MVC configuration, or endpoint mapping, create and own that state inside the module and expose the same instance through a module-owned singleton or internal registry service.
- Do not hide required default services, middleware, endpoint mapping, or post-configuration in the `monica.Add{ModuleName}()` convenience method. Direct and transitive registration must share the same module-owned baseline behavior.
- If a module's built-in behavior needs a non-default phase position, model it as module-owned lifecycle behavior or a named web stage instead of relying on registration-call order.
- When reviewing an existing module, treat builder-entry-only state as a design bug even if the direct registration path currently works.
- Declare business-type work through `TypeDiscoveryPlan<TOptions>` and structural `TypeQuery` expressions. Monica scans the type universe once and commits each module's matches deterministically.

### Required Feature Configuration

When a module needs an explicitly selected provider or strategy, declare a stable feature in `Describe` and satisfy it from each valid registration extension.

```csharp
public sealed class ModuleExample : MonicaModule<ModuleExampleOption>
{
    internal const string STORE_FEATURE = "store";

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(STORE_FEATURE);
    }
}

public static class ModuleExampleRegistrationExtensions
{
    public static ModuleRegistration<ModuleExample, ModuleExampleOption> UseInMemoryStore(
        this ModuleRegistration<ModuleExample, ModuleExampleOption> registration)
    {
        return registration
            .ConfigureServices(context => context.Services.AddSingleton<IStore, InMemoryStore>())
            .SatisfyFeature(ModuleExample.STORE_FEATURE);
    }
}
```

Alternative providers satisfy the same feature. Use `RequireFeature` on a registration when an optional sub-feature introduces a requirement that the baseline module does not own.

## Key Architectural Decisions

1. **Modular Independence**: Each module has minimal dependencies and can function standalone
2. **Automatic Middleware Registration**: Modules automatically register required middleware in correct order
3. **Prevention of Duplicate Registration**: Module system prevents accidental multiple registrations
4. **Strong Typing**: Leverages C# type system for compile-time safety
5. **Performance Optimization**: Reduces reflection usage through cached metadata

## Unified Result Model (Res)

**Scope**: `Res`/`Res<T>` is the lightweight result-envelope model used in Monica entry points that intentionally follow the `IsFailed` consumption pattern. In the current repository guidance, prefer `Res` for UI-facing flows and keep internal infrastructure services on standard .NET returns plus exceptions.

Use `Res<T>` or `Res` only at the boundary that is meant to expose Monica's result-envelope pattern. Do not wrap every internal service in `Res` just for uniformity.

### Quick Reference

```csharp
// Returning success with data
return data;  // Implicit conversion: T => Res<T>

// Returning error
return "Error message";  // Implicit conversion: string => Res<T>

// Explicit methods
return Res.Ok(data);
return Res.Fail("Error message");

// Handling responses
if ((await service.GetDataAsync(id)).IsFailed(out var error, out var data))
{
    // Handle error
    return error;
}
// Use data
```

### Important Rules

1. **Result-envelope entry points** must return `Res<T>` or `Res` — never return null
2. **Internal services** (in `Services/`) must use standard return types and throw exceptions — do not use `Res`
3. **Use implicit conversions** for cleaner code when returning success or error from result-envelope entry points
4. **Handle responses** using the `IsFailed` pattern to extract error and data
5. **Required using**: Include `using Monica.Tool.Results;` where `Res` is used
6. **Typed error details**: Use `AppendMetadata("error", payload)` rather than introducing a separate `ResError` model
7. **Caught exceptions to `Res.Fail`**: When a UI service, Facade, or other result-envelope entry point converts a caught exception into `Res.Fail(...)`, return the full recursive message with `ex.GetMessageRecursively()` instead of only `ex.Message`, so nested exception details are preserved for diagnostics. This usually also requires `using Monica.Core.Extensions;`.

For detailed `Res` type documentation, see `references/res-type-guide.md`.

For service layer patterns (UI vs infrastructure), see `references/module-patterns.md`.

## Hosted Service Development

Monica provides `MoBackgroundService` as a base class for background services with built-in observability.

### Key Principle: Use RecordState, Not Logger

**Use `RecordState` instead of direct Logger calls** for observability. The base class already configures a Logger internally, so direct logging would be redundant.

```csharp
// CORRECT: Use RecordState with explicit LogLevel
RecordState("Operation started", logLevel: LogLevel.Information);
RecordState("Error occurred", logLevel: LogLevel.Error, exception: ex);

// AVOID: Don't use Logger directly (redundant)
// Logger.LogInformation("...");  // Already handled by RecordState
```

### Quick Reference

```csharp
public class MyMonitorService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<MyMonitorService> logger,
    IMyDependency dependency
) : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    public override string ServiceName => nameof(MyMonitorService);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                RecordState("Starting work cycle", logLevel: LogLevel.Information);
                await DoWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                RecordState("Work cycle failed", logLevel: LogLevel.Error, exception: ex);
            }
        }
    }
}
```

### Required Dependencies

| Dependency | Purpose |
|------------|---------|
| `IObservableInstanceManager` | Manages observable state tracking |
| `IOptions<ModuleHostedServiceOption>` | Service configuration options |
| `ILogger<T>` | Optional, passed to base for internal use |

For detailed hosted service patterns including `CoordinatedLeaderService` for leader-aware services, see `references/hosted-service-guide.md`.

## Additional Resources

### Reference Files

- **`references/res-type-guide.md`** - Complete Res result-envelope documentation with implicit conversions and best practices
- **`references/module-patterns.md`** - Module naming conventions, file structure, and implementation patterns
- **`references/hosted-service-guide.md`** - MoBackgroundService patterns, RecordState usage, and CoordinatedLeaderService

### Source Code Reference

- **Res type definition**: `Monica.Tool/Results/Res.cs`
- **Module strategy**: `Monica.Core/Modularity/Abstractions/MonicaModule.cs`
- **Module registration**: `Monica.Core/Modularity/Abstractions/ModuleRegistration.cs`
- **Web module contract**: `Monica.Core/Modularity/Abstractions/IWebModule.cs`
- **MoBackgroundService**: `Monica.Core/Features/HostedServices/MoBackgroundService.cs`
- **CoordinatedLeaderService**: `Monica.RegisterCentre/Core/CoordinatedLeaderService.cs`
