---
name: mo-development
description: This skill should be used when the user asks to "create module", "add module", "module structure", "use Res type", "return Res", "Res.Ok", "Res.Fail", "IsFailed pattern", "module registration", "module dependencies", "module pattern", "Monica architecture", "service layer pattern", "create service", "add service", "create hosted service", "add background service", "MoBackgroundService", "MoHostedService", "RecordState", "hosted service observability", "service state tracking", "CoordinatedLeaderService", or needs guidance on Monica module architecture, the unified response model Res, module registration patterns, Res usage scope (UI vs infrastructure), or hosted service development with observability.
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

Dependencies are automatically registered when a module is added.

## Key Architectural Decisions

1. **Modular Independence**: Each module has minimal dependencies and can function standalone
2. **Automatic Middleware Registration**: Modules automatically register required middleware in correct order
3. **Prevention of Duplicate Registration**: Module system prevents accidental multiple registrations
4. **Strong Typing**: Leverages C# type system for compile-time safety
5. **Performance Optimization**: Reduces reflection usage through cached metadata

## Unified Response Model (Res)

**Scope**: `Res`/`Res<T>` is **only for UI module-related services** — services directly consumed by Blazor components or UI layers. Non-UI / infrastructure modules must use standard .NET patterns (direct return types + exceptions).

UI-facing service methods use the unified response model `Res<T>` or `Res` for return values.

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

1. **UI service methods** must return `Res<T>` or `Res` - never return null
2. **Infrastructure / non-UI service methods** must use standard return types and throw exceptions for errors — do not use `Res`
3. **Use implicit conversions** for cleaner code when returning success or error in UI services
4. **Handle responses** using the `IsFailed` pattern to extract error and data
5. **Required using**: Include `using Monica.Tool.MoResponse;` only in UI service files

For detailed `Res` type documentation, see `references/res-type-guide.md`.

For service layer patterns (UI vs infrastructure), see `references/module-patterns.md`.

## Hosted Service Development

Monica provides `MoBackgroundService` as a base class for background services with built-in observability.

### Key Principle: Use RecordState, Not Logger

**Use `RecordState` instead of direct Logger calls** for observability. The base class already configures a Logger internally, so direct logging would be redundant.

```csharp
// CORRECT: Use RecordState with explicit LogLevel
RecordState("Operation started", givenLogLevel: LogLevel.Information);
RecordState("Error occurred", givenLogLevel: LogLevel.Error, exception: ex);

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
                RecordState("Starting work cycle", givenLogLevel: LogLevel.Information);
                await DoWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                RecordState("Work cycle failed", givenLogLevel: LogLevel.Error, exception: ex);
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
