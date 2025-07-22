# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.


## Architecture Overview

MoLibrary is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

### Module Pattern
Every module follows a consistent pattern:
1. **Module{Name}**: Core module implementation inheriting from `MoModule`
2. **Module{Name}Option**: Configuration options for the module
3. **Module{Name}Guide**: Configuration guide/builder for fluent API
4. **Module{Name}BuilderExtensions**: Extension methods for `IServiceCollection` or `WebApplicationBuilder`

### Core Dependencies
- **MoLibrary.Core**: Foundation for all other modules, contains:
  - `MoModule` base class
  - Module registration system
  - Automatic middleware ordering
  - Core utilities and extensions

### Module Registration
Modules use a unified registration pattern:
```csharp
// Basic registration
builder.ConfigMo{ModuleName}(options => 
{
    // Configure options
});

// With guide for fluent configuration
builder.ConfigMo{ModuleName}()
    .ConfigureOption1()
    .ConfigureOption2();
```

### Key Architectural Decisions
1. **Modular Independence**: Each module has minimal dependencies and can function standalone
2. **Automatic Middleware Registration**: Modules automatically register required middleware in correct order
3. **Prevention of Duplicate Registration**: Module system prevents accidental multiple registrations
4. **Strong Typing**: Leverages C# type system for compile-time safety
5. **Performance Optimization**: Reduces reflection usage through cached metadata

### Module Dependencies
When adding dependencies between modules:
1. Check existing module dependencies in `.csproj` files
2. Maintain minimal coupling between modules
3. Use `MoModule.DependsOn<T>()` to declare module dependencies
4. Dependencies are automatically registered when a module is added

### Static Assets
Static web assets (wwwroot) are handled through:
- Individual module wwwroot folders
- Automatic merging during build
- MudBlazor components in UI modules