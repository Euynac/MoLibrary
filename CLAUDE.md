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

## Development Guidelines

- When creating new UI, always follow the rule in @rules\mo-framework-ui-module-rule.mdc

## Code Quality Principles
- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Split files to avoid overly large single files
- Instead of just fixing errors, use simplified thinking and refactor whenever possible
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.

## Dependency Injection Guidelines
- Always use primary constructor when creating a class with single constructor using dependency injection
  - More details can be read in @rules\primary-constructor.mdc
- 定义Module{Name}Option后，要使用模块Option，直接注入IOption<TModuleOption>或IOptionSnapshot<TModuleOption>使用即可。

## Blazor and MudBlazor Notes
- When using the MudBlazor Icon property in Blazor, you must reference it with an "@" prefix, for example, Icon="@Icons.Material.Filled.Info", instead of Icon="Icons.Material.Filled.Info". Omitting the "@" prefix will cause the Icon not to work.
- In Blazor, JavaScript interop calls cannot be made in OnInitializedAsync because, during static rendering, JavaScript interop calls can only be executed in OnAfterRenderAsync lifecycle method. Additionally, most time-consuming interface initialization tasks should not be placed in OnInitializedAsync, as this can cause page blocking and blank waiting. The correct approach is to perform JavaScript interop and time-consuming initialization in OnAfterRenderAsync(bool firstRender), using the firstRender parameter to ensure execution only during the first render.
- 使用 MudBlazor 的泛型组件（如 MudSwitch、MudChip）时，必须显式指定 T 类型参数，否则会出现类型推断错误。
- 当前项目UI模块使用的框架是MudBlazor 8.9.0
- 不要用<style>标签，必须用CSS isolation。

## Interface Return Value Guidelines
- 对于前端(Controller以及Blazor使用的)的接口的返回值定义，请使用 @统一返回模型Res，使用方式详见 @rules\mo-framework-res-type.mdc 

## MudBlazor Development Notes
- 编写MudBlazor相关代码时，参考迁移文档掌握最新API，且目前提供了引导文档以及源码，能够检索实现。
  - 迁移文档参考路径：
    - @rules\mudblazor\CLAUDE_MUDBLAZOR_OFFICIAL_MIGRATION.md
    - @rules\mudblazor\CLAUDE_MIGRATION_GUIDE.md
    - @rules\mudblazor\CLAUDE_COMPONENT_REFERENCE.md
  - 源码路径：@rules\mudblazor\src

## Available MCP Servers
- **mcp__microsoft-docs__microsoft_docs_search**: MCP Server for searching Microsoft/Azure official documentation. This is particularly useful for finding ASP.NET Core, Blazor, and related documentation and best practices.