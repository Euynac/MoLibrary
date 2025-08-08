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
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.  

## Blazor and MudBlazor Notes
- When using the MudBlazor Icon property in Blazor, you must reference it with an "@" prefix, for example, Icon="@Icons.Material.Filled.Info", instead of Icon="Icons.Material.Filled.Info". Omitting the "@" prefix will cause the Icon not to work.
- In Blazor, JavaScript interop calls cannot be made in OnInitializedAsync because, during static rendering, JavaScript interop calls can only be executed in OnAfterRenderAsync lifecycle method. Additionally, most time-consuming interface initialization tasks should not be placed in OnInitializedAsync, as this can cause page blocking and blank waiting. The correct approach is to perform JavaScript interop and time-consuming initialization in OnAfterRenderAsync(bool firstRender), using the firstRender parameter to ensure execution only during the first render.
- When using MudBlazor's generic components (such as MudSwitch, MudChip, MudTextField), you must explicitly specify the type parameter `T`. Otherwise, type inference errors may occur, or you may encounter the "cannot convert from 'method group' to 'Microsoft.AspNetCore.Components.EventCallback'" error.  

- The current UI module of the project uses MudBlazor **8.9.0**.  
- Do not use `<style>` tags; **CSS isolation** must be used instead.  

## **Interface Return Value Guidelines**  
- For the return value definitions of frontend APIs (used by Controllers and Blazor), always use the **unified response model `Res`**. Refer to `@rules\mo-framework-res-type.mdc` for usage details.  

## **MudBlazor Development Notes**  
- When writing MudBlazor-related code, you must refer to the migration documentation to stay updated on the latest APIs. Currently, guidance documents and source code are available for reference.  
  - **Migration documentation reference paths:**  
    - `@rules\mudblazor\CLAUDE_MUDBLAZOR_OFFICIAL_MIGRATION.md`  
    - `@rules\mudblazor\CLAUDE_MIGRATION_GUIDE.md`  
    - `@rules\mudblazor\CLAUDE_COMPONENT_REFERENCE.md`  
  - **Source code path:** `@rules\mudblazor\src`

## Available MCP Servers
- **mcp__microsoft-docs__microsoft_docs_search**: MCP Server for searching Microsoft/Azure official documentation. This is particularly useful for finding ASP.NET Core, Blazor, and related documentation and best practices.

## Offline Runtime Requirements
**All UI modules and components MUST support offline/intranet environments:**

### Font Management
- **禁止使用在线字体CDN**：不得直接引用 Google Fonts、Adobe Fonts 等在线字体服务
- **本地字体优先**：所有字体文件必须存储在 `wwwroot/fonts/` 目录下
- **字体工具使用**：使用 `@scripts/font-downloader/` 中的工具下载和管理字体文件

### 其他离线要求
- 所有静态资源（CSS、JS、图片等）必须本地化
- 不得依赖任何外部CDN或在线服务
- 必须考虑内网环境下的可用性

### 字体更新流程
1. 使用字体下载器：`python @scripts/font-downloader/font_downloader.py`
2. 将下载的字体文件复制到对应UI模块的 `wwwroot/fonts/` 目录
3. 在主题CSS中配置 `@font-face` 规则引用本地字体文件

## Code Modernization Guidelines
- Unless explicitly instructed to maintain backward compatibility, all refactoring or modifications ​​do not need​​ to consider compatibility with older versions.