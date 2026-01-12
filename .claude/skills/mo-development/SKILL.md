---
name: MoLibrary Development
description: This skill should be used when the user asks to "create module", "add module", "module structure", "use Res type", "return Res", "Res.Ok", "Res.Fail", "IsFailed pattern", "module registration", "module dependencies", "module pattern", "MoLibrary architecture", "service layer pattern", "create service", "add service", or needs guidance on MoLibrary module architecture, the unified response model Res, module registration patterns, or service layer return value conventions.
version: 1.0.0
---

# MoLibrary Development Guide

This skill provides essential guidance for developing modules and services in the MoLibrary framework.

## Architecture Overview

MoLibrary is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

### Module Pattern

Every module follows a consistent pattern with four components:

| Component | Purpose | Example |
|-----------|---------|---------|
| `Module{Name}` | Core module implementation inheriting from `MoModule` | `ModuleSignalR` |
| `Module{Name}Option` | Configuration options for the module | `ModuleSignalROption` |
| `Module{Name}Guide` | Configuration guide/builder for fluent API | `ModuleSignalRGuide` |
| `Module{Name}BuilderExtensions` | Extension methods for `WebApplicationBuilder` | `ModuleSignalRBuilderExtensions` |

### Core Dependencies

**MoLibrary.Core** is the foundation for all other modules, containing:
- `MoModule` base class
- Module registration system
- Automatic middleware ordering
- Core utilities and extensions

## Module Registration

Modules use a unified registration pattern:

```csharp
// Basic registration with options
builder.ConfigModule{ModuleName}(options =>
{
    options.Property1 = value1;
    options.Property2 = value2;
});

// With guide for fluent configuration
builder.ConfigModule{ModuleName}()
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

All service methods must use the unified response model `Res<T>` or `Res` for return values.

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

1. **All service methods** must return `Res<T>` or `Res` - never return null
2. **Use implicit conversions** for cleaner code when returning success or error
3. **Handle responses** using the `IsFailed` pattern to extract error and data
4. **Required using**: Include `using MoLibrary.Tool.MoResponse;`

For detailed `Res` type documentation, see `references/res-type-guide.md`.

## Service Layer Patterns

### Return Value Convention

```csharp
public async Task<Res<UserData>> GetUserAsync(int id)
{
    try
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
        {
            return "User not found";  // Implicit error
        }
        return user;  // Implicit success
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to get user");
        return Res.Fail($"Failed to get user: {ex.Message}");
    }
}
```

### Service Call Pattern

```csharp
// Pattern: Check for failure, extract error and data in one operation
if ((await UserService.GetDataAsync(id)).IsFailed(out var error, out var data))
{
    // Handle error - error contains the failure information
    Logger.LogWarning("Operation failed: {Error}", error.Message);
    return error;  // Propagate error
}

// Success path - data is now available
ProcessData(data);
```

For complete module structure patterns, see `references/module-patterns.md`.

## Additional Resources

### Reference Files

- **`references/res-type-guide.md`** - Complete Res type documentation with implicit conversions and best practices
- **`references/module-patterns.md`** - Module naming conventions, file structure, and implementation patterns

### Source Code Reference

- **Res type definition**: `MoLibrary.Tool/MoResponse/Res.cs`
- **Module base class**: `MoLibrary.Core/Module/MoModule.cs`
