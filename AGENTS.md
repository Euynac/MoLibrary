## Project Overview

Monica is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

## Skills

Proactively invoke these skills when encountering relevant development patterns:

### /mo-development

Invoke when:
- Writing UI service layer methods with `Res` or `Res<T>` return types
- Uncertain about Res implicit conversions or IsFailed pattern
- Determining whether a service should use Res (UI) or standard returns (infrastructure)
- Creating modules (Module{Name}, Option, Guide, BuilderExtensions)
- Configuring module registration or dependencies
- Implementing hosted services (MoBackgroundService, RecordState)

### /mo-ui-development

Invoke when:
- Creating or modifying Blazor components
- Styling MudBlazor components (CSS isolation, ::deep selector)
- Working with MudBlazor APIs or component properties
- Implementing theme customization or dark mode support
- Handling component lifecycle (OnAfterRenderAsync)

**Current MudBlazor version**: 9.0.0 (migrated from 8.9.0)

### /code-simplifier

Invoke when:
- Improving code quality or readability
- Reviewing current git changes, AI-generated code, or a user-specified code area
- Using git diff as an entry point to discover broader related refactoring opportunities unless the user explicitly limits scope
- Planning a refactor before editing, especially when moving behavior into the object that owns the state
- Simplifying/refactoring code while preserving exact behavior
- Making code more object-oriented or moving behavior closer to data/state
- Increasing cohesion and reducing procedural mutation

### Microsoft Documentation Skill

You have access to MCP tools called `microsoft_docs_search`, `microsoft_docs_fetch`, and `microsoft_code_sample_search` - these tools allow you to search through and fetch Microsoft's latest official documentation and code samples, and that information might be more detailed or newer than what's in your training data set.

When handling questions around how to work with native Microsoft technologies, such as C#, ASP.NET Core, Microsoft.Extensions, NuGet, Entity Framework, the `dotnet` runtime - please use these tools for research purposes when dealing with specific / narrowly defined questions that may occur.

## Coding Annotations

- All code annotations (comments, XML doc comments, `<summary>`, `<param>`, `<returns>`, etc.) must be written in English.

## Code Quality Principles

- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Prefer rich models over anemic models: keep behavior on the object that owns the data/state, favor high cohesion and encapsulation, and let services focus on orchestration.
- Split files to avoid overly large single files
- Instead of just fixing errors and introducing complexity merely to solve problems, you **MUST** focus on simplification to enhance code quality. Refactor whenever possible, **WITHOUT** considering backward compatibility. 
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.
- **DO NOT** aim for minimal changes, **ALLOW** breaking changes. Always pursue the **optimal, elegant, simple, and clear design**—be open to large-scale refactoring.

## Res Usage Policy

- `Res` and `Res<T>` are **only for UI module-related services** — services directly consumed by Blazor components or UI layers where the `IsFailed` pattern is used for error handling in the view.
- **Non-UI / infrastructure modules** must use standard .NET patterns: direct return types and throw exceptions (e.g., `KeyNotFoundException`, `FileNotFoundException`, `InvalidOperationException`) for error cases.
- Do not wrap returns in `Res<T>` in infrastructure modules just for consistency — use it only where the UI consumption pattern requires it.

## Dependency Injection Guidelines

- Always use primary constructor when creating a class with single constructor using dependency injection
  - More details can be read in @rules\primary-constructor.mdc
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.

## Development Phase & Optimization Policy

- **Development Stage**: This project is in internal development and has not been released. Backward compatibility is not a concern unless explicitly instructed otherwise.
- **Optimization First**: Always prioritize the most optimal design and implementation approaches. Proactively identify and propose refactoring or redesign opportunities when improvements are possible.
- **Testing Policy**: Unit testing is not required during this phase. Do not include testing-related tasks in planning or implementation unless explicitly requested.

## WSL Environment - dotnet Build Path Issue

**Environment**: This project runs in WSL (Windows Subsystem for Linux) where dotnet CLI is a Windows binary accessed through WSL interoperability.

**Critical Issue**: When using `dotnet build` commands in WSL, you MUST use Windows path format, not Linux/WSL paths.

**Correct Usage**:
```bash
# ✅ CORRECT - Use Windows path format with escaped backslashes
dotnet build D:\\Code\\MoLibrary\\Monica.AI.UI\\Monica.AI.UI.csproj

# ❌ WRONG - WSL path format will fail
dotnet build /mnt/d/Code/MoLibrary/Monica.AI.UI/Monica.AI.UI.csproj

# ❌ WRONG - Relative paths may fail if current directory is incorrect
dotnet build Monica.AI.UI/Monica.AI.UI.csproj
```

**Why This Happens**:
- dotnet CLI in WSL is a Windows program running through interoperability
- MSBuild (invoked by dotnet) cannot understand `/mnt/d/...` Linux-style paths
- It expects native Windows paths like `D:\...`

**Path Conversion** (if needed):
```bash
# Convert WSL path to Windows path
wslpath -w /mnt/d/Code/MoLibrary/Monica.AI.UI/Monica.AI.UI.csproj
# Output: D:\Code\MoLibrary\Monica.AI.UI\Monica.AI.UI.csproj
```

**Always remember**: In WSL, use Windows path format for all dotnet commands.
