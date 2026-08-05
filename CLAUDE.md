## Project Overview

Monica is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

## Skills

Proactively invoke these skills when encountering relevant development patterns:

### /monica-development

Invoke when:
- Writing Facade methods with `Res` or `Res<T>` return types
- Uncertain about Res implicit conversions or IsFailed pattern
- Determining whether code belongs in Facades (Res<T>) or internal Services (exceptions)
- Creating modules (Module{Name}, Option, Guide, BuilderExtensions)
- Configuring module registration or dependencies
- Implementing hosted services (MoBackgroundService, RecordState)
- Structuring module folders (Abstractions, Models, Facades, Services, Providers)

### /monica-ui-development

Invoke when:
- Creating or modifying Blazor components
- Styling MudBlazor components (CSS isolation, ::deep selector)
- Working with MudBlazor APIs or component properties
- Implementing theme customization or dark mode support
- Handling component lifecycle (OnAfterRenderAsync)

**Current MudBlazor version**: 9.0.0 (migrated from 8.9.0)

### /monica-ui-localization

Invoke when:
- Adding, changing, reviewing, or validating Monica UI localization/i18n, user-facing text, `IStringLocalizer<TResource>` usage, `RegisterLocalizedPage(...)` or `RegisterLocalizedCategory(...)` keys, or `zh-CN`/`en-US` resources

## UI Theme Color Contract

- First-party Monica UI colors must use `--mud-palette-*` first, or the small supplemental `--mo-color-*` contract defined in `Monica.UI/wwwroot/css/mo-theme-main.css` when MudBlazor palette roles are not expressive enough.
- `mo-theme-main.css` defines color variables only; do not add shared component styling there as part of color-token cleanup.
- Shared semantic color tokens are intentionally small. Add to the contract only when a cross-module scenario cannot be expressed with `--mud-palette-*`.
- New hardcoded UI colors in Razor, CSS, JS, or C# UI visualization payloads are not allowed; emit `var(--mud-palette-*)` or approved `var(--mo-color-*)` values instead.

### /code-simplifier

Invoke when:
- Cleaning up code changed in the current task or a user-specified area
- Reviewing current changes or AI-generated code for unnecessary complexity, nesting, duplication, or unclear naming
- Simplifying or refactoring code while preserving the active task's intended behavior
- Identifying speculative abstractions introduced by the current change

When this skill is active, its bounded, behavior-preserving scope takes precedence over Monica's general preference for broad refactoring. Review requests report findings without editing; cleanup or refactor requests may change the selected code and directly coupled code required for one coherent simplification.

Do not invoke it for broad architecture review, breaking API redesign, module-boundary restructuring, or speculative refactoring outside the current task. Route Monica module architecture work to `$monica-architecture`.

### Microsoft Documentation Skill

You have access to MCP tools called `microsoft_docs_search`, `microsoft_docs_fetch`, and `microsoft_code_sample_search` - these tools allow you to search through and fetch Microsoft's latest official documentation and code samples, and that information might be more detailed or newer than what's in your training data set.

When handling questions around how to work with native Microsoft technologies, such as C#, ASP.NET Core, Microsoft.Extensions, NuGet, Entity Framework, the `dotnet` runtime - please use these tools for research purposes when dealing with specific / narrowly defined questions that may occur.

### $inspect-dependency-source

Invoke when:
- Debugging behavior that crosses a NuGet or third-party dependency boundary
- Exact SDK or package-version semantics affect the diagnosis
- Third-party source code is needed to validate behavior that public API documentation does not make explicit

Resolve, fetch, and reuse exact dependency source through the user-level shared catalog before relying on a repository's latest branch or ad hoc raw source downloads. Consume the stable `resolve --json` CLI contract; do not read the catalog's internal storage directly.

## Git Commit Requests

When the user asks you to commit changes, read the repository's current commit message guidance first, especially the Conventional Commit rules in `CONTRIBUTING.md`, and use a commit message that follows that policy.

## Monica Repository Coding Annotations

The rules in this section apply only to source files in this Monica repository. Do not carry this
language policy into sibling or consumer repositories; those repositories follow
their own `CLAUDE.md` files and local documentation conventions.

- All code annotations (comments, XML doc comments, `<summary>`, `<param>`, `<returns>`, etc.) must be written in English.
- Add necessary developer-facing documentation, not just code that compiles.
- Public and developer-facing types must have appropriate XML doc comments, especially `Abstractions/`, public `Models/`, `Annotations/`, module `Option` classes, module `Guide` classes, and builder extension methods.
- `Option` properties must explain purpose, effect, important defaults, and when a developer should configure them.
- `Guide` methods and builder extensions must explain what they register or enable, required prerequisites, and notable side effects or usage constraints.
- Public abstractions must explain the contract clearly, including intended usage, lifecycle/ownership expectations, nullability semantics, and exception/timeout behavior when relevant.
- Internal code should also include brief comments for non-obvious logic, especially complex branching, concurrency, normalization rules, caching, retries, or cross-module coordination.
- Do not add comments for obvious code; comments must provide real developer guidance.

## C# Naming Rules

- Private constant fields must use upper snake case, for example `DEFAULT_SEARCH_TOOL_NAME`.

## Code Quality Principles

- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Prefer rich models over anemic models: keep behavior on the object that owns the data/state, favor high cohesion and encapsulation, and let services focus on orchestration.
- Split files to avoid overly large single files
- **DO NOT** aim for minimal changes, **ALLOW** breaking changes. Always pursue the **optimal, elegant, simple, and clear design**—be open to large-scale refactoring.
- Instead of just fixing errors and introducing complexity merely to solve problems, you **MUST** focus on simplification to enhance code quality. Refactor whenever possible.
- **DO NOT** need to consider backward compatibility. 
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.

## Res Usage Policy

- `Res` and `Res<T>` are used in **Facades** — the public entry points defined in infrastructure modules that serve both Minimal API and UI consumers.
- Facades are defined in the **infrastructure module** (e.g., `Monica.AI/RAG/Facades/RAGFacade.cs`), not in UI modules. UI modules inject Facades directly.
- **Internal services** (`Services/`) must use standard .NET patterns: direct return types and throw exceptions (e.g., `KeyNotFoundException`, `InvalidOperationException`) for error cases.
- **Other infrastructure modules** do not consume Facades — they depend on `Abstractions/` interfaces instead.
- **Critical `string` overload trap**: when a facade method returns `Res<string>`, do **not** write `return Res.Ok(content)`. C# will bind to the non-generic `Res.Ok(string hint)` overload, which drops `Res<string>.Data` and can silently break UI behavior. Always use `return Res.Ok<string>(content)` or another explicit generic construction when `T` is `string`.
- See the `monica-architecture` skill for the full architecture specification.

## Dependency Injection Guidelines

- Always use primary constructor when creating a class with single constructor using dependency injection
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.

## Development Phase & Optimization Policy

- **Development Stage**: This project is in internal development and has not been released. Backward compatibility is not a concern unless explicitly instructed otherwise.
- **Optimization First**: Always prioritize the most optimal design and implementation approaches. Proactively identify and propose refactoring or redesign opportunities when improvements are possible.

## Build Warning Policy

- The entire Monica solution must build with **zero warnings**.
- If any warning appears while working on the current task, you **MUST** resolve it before finishing the task.
- Do not leave warnings for later cleanup, and do not silence them with suppression or `NoWarn` unless the user explicitly requires that approach.

## WSL Environment - dotnet Build Path Issue

**Environment**: This project runs in WSL (Windows Subsystem for Linux) where dotnet CLI is a Windows binary accessed through WSL interoperability.

**Critical Issue**: When using `dotnet build` commands in WSL, you MUST use Windows path format, not Linux/WSL paths.

**Correct Usage**:
```bash
# ✅ CORRECT - Use Windows path format with single quotes
dotnet build 'D:\Code\MoLibrary\Monica.AI.UI\Monica.AI.UI.csproj'

# ❌ WRONG - WSL path format will fail
dotnet build /mnt/d/Code/MoLibrary/Monica.AI.UI/Monica.AI.UI.csproj

# ❌ WRONG - Relative paths may fail if current directory is incorrect
dotnet build Monica.AI.UI/Monica.AI.UI.csproj
```

**Path Conversion** (if needed):
```bash
# Convert WSL path to Windows path
wslpath -w /mnt/d/Code/MoLibrary/Monica.AI.UI/Monica.AI.UI.csproj
# Output: D:\Code\MoLibrary\Monica.AI.UI\Monica.AI.UI.csproj
```

**Always remember**: In WSL, use Windows path format for all dotnet commands.

## Solution File Format

- This repository uses `Monica.slnx`.
- Do not assume or create `Monica.sln`.
```bash
dotnet build 'Monica.slnx' -m
```

## WSL Environment - dotnet Parallel Build Rule

Do **NOT** run multiple independent `dotnet build` commands in parallel when the projects share dependencies or output paths.

Use MSBuild parallelism **inside one build** with `-m`, not by starting several `dotnet build` processes at the same time. Otherwise file locks may cause errors such as `CS2012` or `MSB3026`.
