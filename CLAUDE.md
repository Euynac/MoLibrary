# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.


## Architecture Overview

MoLibrary is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

### Module Pattern
Every module follows a consistent pattern:
1. **Module{Name}**: Core module implementation inheriting from `MoModule`
2. **Module{Name}Option**: Configuration options for the module
3. **Module{Name}Guide**: Configuration guide/builder for fluent API
4. **Module{Name}BuilderExtensions**: Extension methods for `WebApplicationBuilder` to config module.

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
builder.ConfigModule{ModuleName}(options => 
{
    // Configure options
});

// With guide for fluent configuration
builder.ConfigModule{ModuleName}()
    .GuideMethod1()
    .GuideMethod2();
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
3. Use `DependsOnModule<{ModuleName}Guide>().Register();` to declare module dependencies
4. Dependencies are automatically registered when a module is added

### Static Assets
Static web assets (wwwroot) are handled through:
- Individual module wwwroot folders
- Automatic merging during build
- MudBlazor components in UI modules


## Code Quality Principles
- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Split files to avoid overly large single files
- Instead of just fixing errors, use simplified thinking and refactor whenever possible
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.

## Dependency Injection Guidelines
- Always use primary constructor when creating a class with single constructor using dependency injection
  - More details can be read in @rules\primary-constructor.mdc
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.  

## **Blazor and MudBlazor UI Development**

For comprehensive guidance on Blazor UI development with MudBlazor in MoLibrary, use the **MoLibrary UI Development** skill.

The skill covers:
- CSS isolation patterns and `::deep` selector usage
- MudBlazor component best practices (Icon prefix, type parameters)
- Component lifecycle (OnAfterRenderAsync patterns)
- Theme customization and CSS variables
- MudBlazor 8.9.0 migration guide
- Offline/intranet requirements

**Current MudBlazor version**: 8.9.0

## **Interface Return Value Guidelines**
- For the return value definitions of frontend APIs (used by Controllers and Blazor), always use the **unified response model `Res`**. Refer to `@rules\mo-framework-res-type.mdc` for usage details.

## Available MCP Servers
- **mcp__microsoft-docs__microsoft_docs_search**: MCP Server for searching Microsoft/Azure official documentation. This is particularly useful for finding ASP.NET Core, Blazor, and related documentation and best practices.

## Development Phase & Optimization Policy
- **Development Stage**: This project is in internal development and has not been released. Backward compatibility is not a concern unless explicitly instructed otherwise.
- **Optimization First**: Always prioritize the most optimal design and implementation approaches. Proactively identify and propose refactoring or redesign opportunities when improvements are possible.
- **Testing Policy**: Unit testing is not required during this phase. Do not include testing-related tasks in planning or implementation unless explicitly requested.