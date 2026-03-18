---
name: mo-development
description: This skill should be used when the user asks to "create module", "add module", "module structure", "use Res type", "return Res", "Res.Ok", "Res.Fail", "IsFailed pattern", "module registration", "module dependencies", "module pattern", "Monica architecture", "service layer pattern", "create service", "add service", "create hosted service", "add background service", "MoBackgroundService", "MoHostedService", "RecordState", "hosted service observability", "service state tracking", "CoordinatedLeaderService", "GetRequestedConfigMethodKeys", "required config", "required configuration methods", or needs guidance on Monica module architecture, the unified response model Res, module registration patterns, Res usage scope (UI vs infrastructure), required Guide configuration validation, or hosted service development with observability.
version: 1.0.0
---

# Monica Development Guide

This skill provides essential guidance for developing modules and services in the Monica framework.

## Architecture Overview

Monica is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

### Module Pattern

Every module follows a consistent pattern with four components in one `Module{Name}.cs` file, which is located in the `Modules` folder of each project.

| Component | Purpose | Example |
|-----------|---------|---------|
| `Module{Name}` | Core module implementation inheriting from `MoModule` | `ModuleSignalR` |
| `Module{Name}Option` | Configuration options for the module | `ModuleSignalROption` |
| `Module{Name}Guide` | Configuration guide/builder for fluent API | `ModuleSignalRGuide` |
| `Module{Name}BuilderExtensions` | Extension methods for `WebApplicationBuilder` | `ModuleSignalRBuilderExtensions` |

### Core Dependencies

**Monica.Core** is the foundation for all other modules, containing:
- `MoModule` base class
- Module registration system
- Automatic middleware ordering
- Core utilities and extensions

## Module Registration

Modules use a unified registration pattern:

```csharp
// Basic registration with options
Mo.Add{ModuleName}(options =>
{
    options.Property1 = value1;
    options.Property2 = value2;
});

// With guide for fluent configuration
Mo.Add{ModuleName}()
    .GuideMethod1()
    .GuideMethod2();
```

### Module Dependencies

When a module depends on other modules:

```csharp
public override void ClaimDependencies()
{
    DependsOnModule<ModuleOtherGuide>().Register();
    DependsOnModule<ModuleAnotherGuide>().Register();
}
```

Modules declare dependencies by overriding `ClaimDependencies()` on `MoModule<TModuleSelf, TModuleOption, TModuleGuide>`.

Dependencies are automatically registered when a module is added.

### Required Configuration Methods (GetRequestedConfigMethodKeys)

When a module's services depend on registrations that must come from Guide methods (e.g., choosing a store implementation), the Guide class must override `GetRequestedConfigMethodKeys` to declare those requirements. The module system validates at startup that all required keys have been satisfied, throwing a clear error if any are missing.

**Pattern**: Define `private const string` keys, return them from `GetRequestedConfigMethodKeys`, and pass `key:` to `ConfigureServices`/`ConfigureEmpty` in the guide methods that satisfy each requirement.

```csharp
public class ModuleExampleGuide
    : MoModuleGuide<ModuleExample, ModuleExampleOption, ModuleExampleGuide>
{
    // 1. Define constants for required configuration keys
    private const string CONFIG_STORE = nameof(CONFIG_STORE);
    private const string CONFIG_PROVIDER = nameof(CONFIG_PROVIDER);

    // 2. Declare which keys are required
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_STORE, CONFIG_PROVIDER];
    }

    // 3. Guide methods satisfy requirements by passing key:
    //    Multiple methods can share the same key (alternatives)
    public ModuleExampleGuide UseInMemoryStore()
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IStore, InMemoryStore>();
        }, key: CONFIG_STORE);  // Satisfies CONFIG_STORE
        return this;
    }

    public ModuleExampleGuide UseCustomStore<T>() where T : class, IStore
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.AddSingleton<IStore, T>();
        }, key: CONFIG_STORE);  // Also satisfies CONFIG_STORE
        return this;
    }

    // Use ConfigureEmpty when the method doesn't register services
    // but still needs to mark the requirement as satisfied
    public ModuleExampleGuide UseDefaultProvider()
    {
        ConfigureEmpty(CONFIG_PROVIDER);
        return this;
    }
}
```

**Key rules**:
- If a service registered in `Module.ConfigureServices` depends on a DI registration that only comes from a Guide method, that Guide method's key **must** be in `GetRequestedConfigMethodKeys`
- Alternative methods (e.g., `UseInMemory` vs `UseCustom`) share the same key constant
- Use `ConfigureEmpty(KEY)` when a method satisfies a requirement without registering services

## Key Architectural Decisions

1. **Modular Independence**: Each module has minimal dependencies and can function standalone
2. **Automatic Middleware Registration**: Modules automatically register required middleware in correct order
3. **Prevention of Duplicate Registration**: Module system prevents accidental multiple registrations
4. **Strong Typing**: Leverages C# type system for compile-time safety
5. **Performance Optimization**: Reduces reflection usage through cached metadata

## Unified Response Model (Res)

**Scope**: `Res`/`Res<T>` is used in **Facades** — the public entry points defined in infrastructure modules that serve both Minimal API and UI consumers. Internal services within modules must use standard .NET patterns (direct return types + exceptions).

Facade methods use the unified response model `Res<T>` or `Res` for return values. UI modules inject Facades directly — no separate UI service layer needed for data access.

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

1. **Facade methods** must return `Res<T>` or `Res` — never return null
2. **Internal services** (in `Services/`) must use standard return types and throw exceptions — do not use `Res`
3. **Use implicit conversions** for cleaner code when returning success or error in Facades
4. **Handle responses** using the `IsFailed` pattern to extract error and data
5. **Required using**: Include `using Monica.Tool.MoResponse;` in Facade files
6. **UI modules inject Facades directly** — no separate UI service layer needed for data access

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

- **`references/res-type-guide.md`** - Complete Res type documentation with implicit conversions and best practices
- **`references/module-patterns.md`** - Module naming conventions, file structure, and implementation patterns
- **`references/hosted-service-guide.md`** - MoBackgroundService patterns, RecordState usage, and CoordinatedLeaderService

### Source Code Reference

- **Res type definition**: `Monica.Tool/MoResponse/Res.cs`
- **Module base class**: `Monica.Core/Module/MoModule.cs`
- **MoBackgroundService**: `Monica.Core/Features/HostedServices/MoBackgroundService.cs`
- **CoordinatedLeaderService**: `Monica.RegisterCentre/Core/CoordinatedLeaderService.cs`
