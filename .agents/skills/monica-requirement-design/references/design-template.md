# {Requirement Title} - Architectural Design

> Requirement: `.pending/{NNN}-{name}/requirements.md`
> Created: {date}
> Last Updated: {date}

## Overview

{High-level summary of the architectural approach. What is being built and why this design was chosen.}

## Architecture

### Module Placement

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Module type | New module / Extension / Cross-cutting | {Why} |
| Project name | `Monica.{Name}` | {Why} |
| UI module | Mixed / Standalone / Framework / None | {Why} |
| Runtime kind | `ModuleBase` / `WebModuleBase` / `WebModuleBase` with downgrade | {Why this lifecycle is needed} |

### High-Level Architecture

{Describe the overall architecture. Include component relationships and data flow.}

```
{mermaid diagram or description of component relationships}
```

## Abstractions & Interfaces

{Define the key interfaces and abstractions. Use C# code blocks.}

```csharp
public interface I{ServiceName}
{
    {Method signatures}
}
```

### Models

```csharp
/// <summary>
/// {Description of the model.}
/// </summary>
public record {ModelName}(
    {Type} {Property1},
    {Type} {Property2}
);
```

## Module Structure

### Module Components

| Component | Class Name | Purpose |
|-----------|-----------|---------|
| Module | `Module{Name}` | {Purpose} |
| Option | `Module{Name}Option` | {Purpose} |
| Guide | `Module{Name}Guide` | {Purpose} |
| Builder Extension | `monica.Add{Name}()` | {Purpose} |

### Module Registration

```csharp
monica.Add{Name}(options =>
{
    // Configuration
});
```

### Module Dependencies

Modules declare dependencies by overriding `ClaimDependencies()` on `ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>` or `WebModuleBase<TModuleSelf, TModuleOption, TModuleGuide>`.

```csharp
public override void ClaimDependencies()
{
    DependsOnModule<{DependencyGuide}>().Register();
}
```

## Directory Structure

```
Monica.{Name}/
├── Modules/
│   └── Module{Name}.cs              # Module, Option, Guide, BuilderExtensions
├── Abstractions/
│   └── I{Feature}Service.cs         # Public contract
├── Facades/
│   └── {Name}Facade.cs              # Host-facing Res<T> entry point
├── Services/
│   └── {Feature}Service.cs          # Implementation
├── Models/
│   └── {Model}.cs                   # Data models
└── Monica.{Name}.csproj
```

<!-- Include the following section only if a UI module is needed -->

```
Monica.{Name}.UI/
├── Modules/
│   └── Module{Name}UI.cs
├── Pages/
│   └── UI{Name}Page.razor
├── UI{Name}/
│   ├── Components/
│   ├── Dialogs/
│   ├── State/
│   └── Support/
├── Localization/
└── Monica.{Name}.UI.csproj
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| {Package} | {Version} | {Purpose} |

### Monica Module Dependencies

| Module | Relationship | Purpose |
|--------|-------------|---------|
| Monica.{Name} | Depends on | {Purpose} |

## Service Layer Design

### Infrastructure Services

{Services that use standard .NET patterns (direct returns + exceptions).}

```csharp
public class {Feature}Service(
    ILogger<{Feature}Service> logger,
    {IDependency} dependency
) : I{Feature}Service
{
    public async Task<{ReturnType}> {Method}Async({params})
    {
        // Implementation
    }
}
```

### Facade Entry Points

```csharp
public class {Feature}Facade(
    ILogger<{Feature}Facade> logger,
    I{Feature}Service featureService
)
{
    public async Task<Res<{ReturnType}>> {Method}Async({params})
    {
        // Wrap infrastructure service calls with Res for UI / host consumption
    }
}
```

UI components inject Facades directly. Do not introduce a separate UI service layer unless the user explicitly asks for a different architecture.

## Implementation Plan

{Ordered steps for implementation. Each step should be independently completable.}

### Phase 1: Core Infrastructure

1. **Create project and module skeleton**
   - Create `Monica.{Name}/` project
   - Implement Module, Option, Guide, BuilderExtensions
   - Add to solution

2. **Define interfaces and models**
   - Create public contracts in `Abstractions/`
   - Define data models in `Models/`

3. **Implement services**
   - Implement core service logic
   - Register services in module's `ConfigureServices`

### Phase 2: Integration

4. **Wire up dependencies**
   - Configure dependencies with existing modules
   - Set up endpoints (if applicable)

### Phase 3: UI (if applicable)

5. **Create UI module**
   - Create UI project/module
   - Reuse Facades directly from the infrastructure module
   - Build Blazor components and pages

## Design Decisions

| Decision | Options Considered | Choice | Rationale |
|----------|-------------------|--------|-----------|
| {Decision} | {Option A}, {Option B} | {Choice} | {Why} |

## Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| {Risk} | High/Medium/Low | High/Medium/Low | {Mitigation} |

## Open Items

- [ ] {Open item 1}
- [ ] {Open item 2}
