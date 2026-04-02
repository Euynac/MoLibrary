---
name: mo-architecture
description: This skill should be used when the user asks to "design module structure", "plan module architecture", "review module layout", "create new module", "refactor module structure", "module folder structure", "module boundaries", "facade pattern", "internal vs public", "feature-first", "annotations folder", "developer-facing attributes", "where to put attributes", "page decomposition", "page too large", "extract page state", "模块架构", "架构设计", "模块结构", "文件夹结构", or needs guidance on Monica module directory layout, layer responsibilities, dependency direction, public/internal boundaries, Facade placement, Provider separation, Annotations placement, page decomposition rules, Features pattern for bundled sub-modules, or Mixed/Standalone/Composite UI module patterns.
version: 1.1.2
---

# Monica Unified Module Architecture

This skill defines the canonical architecture for all Monica modules. It is the single source of truth for module structure decisions.

Other skills reference this skill:
- `mo-development` — for module registration, Res type, service patterns
- `mo-ui-development` — for UI component, page, and styling patterns

## Quick Decision Guide

**Creating a new module?** → Use the Infrastructure Module Template below.
**Adding UI to an existing module?** → Choose Mixed (lightweight UI) or Standalone (complex UI).
**Module getting large?** → Use the Features Pattern (only if 40+ files or 3+ independent sub-domains).
**Need developer-facing attributes?** → Place in `Annotations/` (public layer).
**Page file getting large?** → Check the Page Decomposition Rules.
**Unsure where a file goes?** → Check the Standard Layer Names table.
**Unsure if something is public or internal?** → Check the Visibility Rules table.
**Working in `Modules/`?** → See `Modules/ Is Registration Only`.
**Grouping related files?** → Use prefix naming. Only create sub-folders for 6+ files. See Folder Depth & Grouping Rules.
**Folder depth reaching 4 levels?** → Stop. Use prefix naming instead. Max depth is 3.

## Core Principles

### 1. Feature-First, Layers Inside Features

Split by feature at the project level first, then use standard layer folders inside each feature.

This is **Feature-First + Layer-Inside-Feature** — it prevents any single folder from becoming a dumping ground.
Feature folder names should usually stay aligned with the module or sub-module name, following the `Monica.DevOps/` style such as `Git/`, `K8S/`, and `FileOps/`, unless a special requirement suggests a different name.

### Localization Placement Exception

Localization resources are a project-level concern in Monica. Even when the rest of a module uses feature folders, keep resource marker classes and JSON files under the project root `Localization/` folder, not under feature subfolders. This keeps the resource namespace, embedded resource path, and Monica localization validation workflow consistent.

### 2. Facades Are the Only Public Entry Point for API and UI

Every module exposes capabilities through `Facades/`:

- Return `Res` / `Res<T>` (unified response model)
- Consumed by Minimal API endpoints and UI components directly
- Delegate to internal `Services/` for implementation
- Must NOT contain substantial business logic

Facades live in the **infrastructure module**, not the UI module:

```
Minimal API  ──→  Facade (Res<T>)  ──→  Services (internal)
UI Component ──→  Facade (Res<T>)  ──→  Services (internal)
Other Module ──→  Abstractions (interfaces)  ←── Services implement
```

### 3. Public vs Internal Boundary

**Public surface** (consumed by external modules, API, UI):
- `Abstractions/` — interfaces for cross-module dependency
- `Annotations/` — developer-facing attributes for declarative configuration
- `Models/` — shared data contracts
- `Facades/` — `Res<T>` entry points for API/UI

**Internal implementation** (hidden inside the module):
- `Abstractions/Internal/` — internal-only contracts
- `Models/Internal/` — internal-only data types
- `Services/` — all implementation logic
- `Providers/` — pluggable strategy implementations

The `Internal/` sub-folder convention makes this boundary visible in the directory structure.

### 4. Dependency Direction

Allowed:
```
UI Page / Minimal API → Facade (Res<T>) → Services → Providers
Other Module → Abstractions (interfaces) + Models (public)
```

Forbidden:
- Service → Facade
- Provider → Facade or UI
- Model → Service
- Module registration → business implementation details

### 5. Provider Is a Separate Layer

Providers encapsulate vendor SDKs, external systems, file I/O, databases, vector stores. They implement `Abstractions/` interfaces and are always in their own `Providers/` folder with sub-folders per provider.

Providers do NOT orchestrate business workflows, manage page state, or return `Res<T>`.

### 6. Modules/ Is Registration Only

`Modules/` contains only Module, Option, Guide, BuilderExtensions, dependency declarations, and DI registrations. No business logic.

For Monica, these registration artifacts are typically **co-located in one file per module**:
- Infrastructure module: `Modules/Module{Name}.cs`
- UI module: `Modules/Module{Name}UI.cs`

Keep `Module{Name}`, `Module{Name}Option`, `Module{Name}Guide`, and related builder extension methods together in that single file by default.

Do NOT proactively split them into separate files such as:
- `Module{Name}Option.cs`
- `Module{Name}Guide.cs`
- `Module{Name}BuilderExtensions.cs`

Only split a module registration file when the user explicitly asks for that refactor.

### 7. Utils Is the Unified Utility Folder

`Utils/` replaces `Helpers/`, `Tools/`, `Utilities/`, `Common/`, `Misc/`. Only one utility folder name is allowed.

Exception: `Tools/` is reserved for AI tool providers in Monica.AI modules.

## Folder Depth & Grouping Rules

### Maximum Depth: 3 Levels

The maximum folder depth from project root is **3 levels**: `Feature/Layer/SubLayer/`. Never create a 4th nesting level.

```
✅ Authorization/Services/Support/PolicyRequirement.cs        (3 levels)
❌ Authorization/Services/Support/Policies/PolicyRequirement.cs (4 levels — NEVER)
```

### Prefix Naming Over Sub-Folders

**Prefer prefix naming to group related files** within a folder instead of creating sub-folders. This leverages IDE alphabetical sorting to achieve visual grouping without folder overhead.

This is the standard pattern used by ASP.NET Core, EF Core, and MudBlazor:
```
# ASP.NET Core — Authentication/ has 7+ files, zero sub-folders
AuthenticationHandler.cs
AuthenticationMiddleware.cs
AuthenticationScheme.cs
AuthenticationSchemeBuilder.cs
AuthenticationSchemeOptions.cs
AuthenticationSchemeProvider.cs
```

Apply to Monica modules:
```
# ✅ Prefix naming — flat, scannable, IDE-friendly
Authorization/Services/Support/
├── InterceptionAuthorizer.cs
├── InterceptionRegistrar.cs
├── PermissionBitChecker.cs
├── PermissionBitCheckerManager.cs
├── PolicyEnumRequirement.cs
├── PolicyEnumRequirementHandler.cs
├── PolicyEnumProvider.cs
└── AuthorizationRes.cs

# ❌ Sub-folder explosion — 4+ levels, 1-2 files per folder
Authorization/Services/Support/Interception/AuthorizationInterceptor.cs
Authorization/Services/Support/Policies/EnumPermissionRequirement.cs
Authorization/Services/Support/PermissionBits/PermissionBitChecker.cs
Authorization/Services/Support/Responses/MoAuthorizationRes.cs
```

### When to Use Sub-Folders vs Prefixes

| Condition | Strategy |
|-----------|----------|
| Group has ≤ 5 files | Prefix naming, keep flat |
| Group has 6–10 files | Consider sub-folder |
| Group has > 10 files | Use sub-folder |
| Would create 4th nesting level | **Always** use prefix, never sub-folder |
| `Internal/` boundary marker | Sub-folder (this is a visibility boundary, not grouping) |
| `Providers/{ProviderName}/` | Sub-folder (each provider is a replaceable unit) |

### Restructuring Scope Rule

Architecture restructuring means **moving existing files** into the correct layer folders. It does NOT include:
- Splitting classes or extracting new interfaces
- Changing public API surface
- Adding new abstractions that didn't exist before

Those are separate tasks requiring explicit user approval.

## Standard Layer Names

| Folder | Purpose | Visibility |
|--------|---------|------------|
| `Abstractions/` | Interfaces, abstract base classes | Public |
| `Abstractions/Internal/` | Internal-only contracts | Private |
| `Annotations/` | Developer-facing attributes for declarative configuration | Public |
| `Models/` | Records, DTOs, enums, value objects | Public |
| `Models/Internal/` | Internal-only data types | Private |
| `Facades/` | Thin orchestration, returns `Res<T>` | Public |
| `Services/` | Implementation logic | Private |
| `Services/Support/` | Registry, Resolver, Coordinator, Policy, Factory — use prefix naming to group | Private |
| `Providers/` | Pluggable strategy implementations | Private |
| `Modules/` | Module registration units | Public |
| `Extensions/` | Extension methods | Depends on usage |
| `Events/` | Domain/integration events | Public |
| `Exceptions/` | Module-specific exception types | Public |
| `Utils/` | Pure utility functions | Private |

Group files within a layer using **prefix naming**. Only create sub-folders when a group exceeds 5 files (see Folder Depth & Grouping Rules above).

## Infrastructure Module Template

```
Monica.{Name}/
├── Modules/
│   └── Module{Name}.cs                  # Consolidated module registration file
├── Abstractions/
│   ├── I{Feature}.cs
│   └── Internal/                        # (visibility boundary — sub-folder allowed)
│       └── I{InternalContract}.cs
├── Annotations/                         # (optional, developer-facing attributes)
│   └── {Name}Attribute.cs
├── Models/
│   ├── {Entity}.cs
│   └── Internal/                        # (visibility boundary — sub-folder allowed)
│       └── {InternalModel}.cs
├── Facades/
│   └── {Name}Facade.cs
├── Services/
│   ├── {Feature}Service.cs
│   └── Support/                         # Use prefix naming to group, NOT sub-folders
│       ├── {Group}Registry.cs           # e.g., ChunkerRegistry.cs
│       ├── {Group}Resolver.cs           # e.g., ChunkerResolver.cs
│       ├── {Group}Coordinator.cs        # e.g., IndexCoordinator.cs
│       └── {Group}Policy.cs             # e.g., PermissionPolicy.cs
├── Providers/
│   └── {ProviderName}/                  # (replaceable unit — sub-folder allowed)
│       └── {Name}Provider.cs
├── Extensions/                          # (optional)
├── Events/                              # (optional)
├── Exceptions/                          # (optional)
└── Utils/                               # (optional)
```

**Template is maximum structure, not minimum.** Most modules only need 2–3 of these layers. Do NOT create empty or near-empty layers. If a module only has Services and Abstractions, that's fine.

### Facade Rules

- Return `Res` / `Res<T>` exclusively
- Stay lightweight (~200 lines max per file), delegate to `Services/`
- Handle parameter normalization, use-case orchestration, error-to-Res conversion
- Naming: `{Feature}Facade.cs`

### Service Rules

- Each service expresses one clear capability
- Use standard .NET return types and exceptions (NOT `Res<T>`)
- Only consumed by Facades and other Services within the same module
- Recommended naming: `KnowledgeBaseService`, `DocumentIndexingService`
- Avoid: `RAGManager`, `RAGHelper`, `RAGCoreService`

### Support Service Rules

Suitable types: Registry, Resolver, Coordinator, Policy, Normalizer, Factory.

Use **prefix naming** to group related support files (e.g., `PolicyRequirement.cs`, `PolicyHandler.cs`, `PolicyProvider.cs`). Do NOT create sub-folders within `Support/` unless a single group exceeds 5 files.

## Features Pattern (Bundled Sub-Modules)

**When to use**: Only when a module has **40+ files** or **3+ clearly independent sub-domains with their own Facades**. For smaller modules, standard flat layers are preferred.

```
Monica.{Name}/
├── Modules/
│   ├── Module{Name}.cs                  # Consolidated root module registration file
│   └── Module{SubFeature}.cs            # Consolidated sub-feature registration file
├── {FeatureA}/                          # OR under Features/
│   ├── Abstractions/
│   ├── Models/
│   ├── Facades/
│   └── Services/
│       └── Support/                     # Prefix naming inside, no sub-folders
├── {FeatureB}/
│   ├── Abstractions/
│   ├── Models/
│   └── Services/
├── Extensions/                          # (optional, project-level)
└── Utils/                               # (optional, project-level)
```

Rules:
- Each feature follows the same layer convention as a top-level module
- Cross-feature shared types go in project-level folders
- Features must NOT depend on another feature's internal `Services/`
- Only include layers that have files — do not create empty layers

Two valid approaches:
- **Feature folders at project root** — when features are the primary unit (e.g., `Monica.AI/Chat/`, `Monica.AI/RAG/`)
- **`Features/` container** — when the module also has significant project-level code (e.g., `Monica.Core/Features/`)

## UI Module Structure

### Core Rule

UI modules are pure presentation layers:
- Inject Facades from the infrastructure module directly
- Do NOT have their own service layer for data access
- Maximize reuse of Models from infrastructure module's public `Models/`
- Always use `UI{Name}/` feature directories

### Standalone UI Module

```
Monica.{Name}.UI/
├── Modules/
│   └── Module{Name}UI.cs                # Consolidated UI module registration file
├── Pages/
│   ├── {Name}Page.razor
│   └── {Name}Page.razor.css
├── Components/
├── Dialogs/                         # (optional)
├── Models/                          # View-only (minimize — reuse Facade Models)
├── State/                           # Browser/page/session state
└── Support/                         # Resolvers, formatters, coordinators
├── Localization/
└── wwwroot/                             # (optional)
```

### Composite UI Module

When one UI project hosts multiple sub-modules:

```
Monica.{Family}.UI/
├── Modules/
│   ├── Module{FeatureA}UI.cs            # Consolidated registration file for FeatureA UI
│   ├── Module{FeatureB}UI.cs            # Consolidated registration file for FeatureB UI
│   └── Module{Family}UI.cs              # (optional, aggregator) consolidated registration file
├── Pages/
│   ├── UI{FeatureA}Page.razor
│   └── UI{FeatureB}Page.razor
├── UI{FeatureA}/
│   ├── Components/
│   ├── Dialogs/
│   ├── Models/
│   ├── State/
│   └── Support/
├── UI{FeatureB}/
│   ├── Components/
│   ├── Models/
│   ├── State/
│   └── Support/
├── Localization/
└── wwwroot/
```

### UI Folder Responsibilities

| Folder | What belongs | What does NOT belong |
|--------|-------------|---------------------|
| `Pages/` | Route pages, page composition, lifecycle | Business orchestration, SDK calls |
| `UI{Name}/Components/` | Reusable Blazor components | Page routing, business entry points |
| `UI{Name}/Dialogs/` | Dialog components | Non-dialog components |
| `UI{Name}/Models/` | ViewModels, DialogModels (minimize) | Infrastructure models |
| `UI{Name}/State/` | Browser/page/session/table state | Business orchestration |
| `UI{Name}/Support/` | Resolvers, formatters, coordinators | Data access (use Facade) |
| `Localization/` | Resource markers, JSON files | Business logic |

### UI State / Support Classification

| Type | Folder | Examples |
|------|--------|---------|
| Page/session state | `State/` | `ChatSessionStateManager` |
| Browser persistence | `State/` | `RAGBrowserState`, `TableStateStorage` |
| Orchestration helpers | `Support/` | `RAGBatchIndexCoordinator` |
| Resolution/lookup | `Support/` | `RAGMarkdownDocumentResolver` |
| Format conversion | `Support/` | `ChunkHighlightFormatter` |

## Mixed Module Structure

Mixed modules contain both infrastructure and UI in one project.

```
Monica.{Name}/
├── Modules/
│   ├── Module{Name}.cs                  # Consolidated infrastructure registration file
│   └── Module{Name}UI.cs                # Consolidated UI registration file
├── Abstractions/
│   └── Internal/
├── Models/
│   └── Internal/
├── Facades/
├── Services/
│   └── Support/                         # Prefix naming inside, no sub-folders
├── Providers/
│   └── {ProviderName}/
├── Pages/
│   └── UI{Name}Page.razor
├── UI{Name}/
│   ├── Components/
│   ├── Dialogs/
│   ├── Models/
│   ├── State/
│   └── Support/
├── Localization/
├── Extensions/                          # (optional)
├── Utils/                               # (optional)
└── wwwroot/                             # (optional)
```

Boundary rules:
- UI components inject Facades directly — no intermediate service layer
- Being in the same assembly does NOT relax layering rules
- UI code must NOT reach into `Services/` or `Providers/`

When to use Mixed: UI is lightweight, tightly coupled lifecycle, no separate packaging needed.
When to upgrade to Standalone: UI pages growing, multiple UI sub-features, separate deployment needed.

## Page Decomposition Rules

Pages are thin composition shells — they wire up components and delegate state. They do not own business logic, polling loops, or complex state machines.

### Size Limits

| Element | Guideline |
|---------|-----------|
| Page markup | ~100–200 lines |
| Page `@code` block | ~50–150 lines (lifecycle + event wiring only) |
| Total page file | ≤ 350 lines |

### What to Extract and Where

| Concern | Extract to | Example |
|---------|-----------|---------|
| Complex UI sections | `UI{Name}/Components/` | `DocumentQueueSection.razor` |
| Dialog flows | `UI{Name}/Dialogs/` | `CreateKnowledgeBaseDialog.razor` |
| Page/selection/polling state | `UI{Name}/State/` | `RAGManagePageState.cs` |
| Loading orchestration | `UI{Name}/State/` | `RAGLoadingStateManager.cs` |
| Format/display helpers | `UI{Name}/Support/` | `EmbeddingModelDisplayResolver.cs` |
| Batch coordination | `UI{Name}/Support/` | `RAGBatchIndexCoordinator.cs` |

### Page Composition Pattern

```razor
@* Page is a thin shell: inject state, compose components *@
@inject RAGManagePageState PageState

<div class="rag-manage-page">
    <KnowledgeBaseListPanel />
    <KnowledgeBaseDetailPanel />
</div>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await PageState.InitializeAsync();
    }

    public void Dispose() => PageState.Dispose();
}
```

### Red Flags (Extract Immediately)

- Page has > 10 private fields
- Page owns `CancellationTokenSource` or polling loops
- Page has `Interlocked` or concurrency primitives
- Page has > 5 `Can*()` guard methods
- Page duplicates loading skeleton markup across panels
- UI module has a flat root-level `Services/` folder

For a concrete anti-pattern case study, see `references/refactoring-examples.md`.

## Naming Conventions

### Module Registration

| Component | Pattern | Example |
|-----------|---------|---------|
| Module class | `Module{Name}` | `ModuleRAG` |
| Options class | `Module{Name}Option` | `ModuleRAGOption` |
| Guide class | `Module{Name}Guide` | `ModuleRAGGuide` |
| Builder extension | `Mo.Add{Name}()` | `Mo.AddRAG()` |

### Service Naming

| Type | Pattern | Examples |
|------|---------|---------|
| Facade | `{Feature}Facade` | `RAGFacade`, `AIChatFacade` |
| Business service | `{Feature}Service` | `DocumentIndexingService` |
| Registry | `{Feature}Registry` | `ChunkerRegistry` |
| Resolver | `{Feature}Resolver` | `RAGEmbeddingBindingResolver` |
| Coordinator | `{Feature}Coordinator` | `RAGIndexStateCoordinator` |
| Provider | `{Strategy}{Capability}Provider` | `FileDocumentIndexStateStore` |

Avoid vague names: `Manager`, `Handler`, `Helper`, `Core`.

### UI Naming

| Component | Pattern | Example |
|-----------|---------|---------|
| UI module | `Module{Name}UI` | `ModuleRAGUI` |
| UI folder | `UI{Name}/` | `UIRAG/` |
| Page | `UI{Name}Page` | `UIRAGManagePage` |
| Route URL | `/{name}-{action}` | `/rag-manage` |

## Visibility Rules

| Layer | Default | Rationale |
|-------|---------|-----------|
| `Abstractions/` | `public` | External modules depend on these |
| `Abstractions/Internal/` | `internal` | Module-internal contracts |
| `Annotations/` | `public` | Developer-facing declarative attributes |
| `Models/` | `public` | Shared data contracts |
| `Models/Internal/` | `internal` | Module-internal data types |
| `Facades/` | `public` | API + UI entry points |
| `Services/` | `internal` | Implementation details |
| `Providers/` | `internal` | Pluggable but internal |
| `Utils/` | `internal` | Module-internal utilities |

## Additional Resources

### Reference Files

- **`references/refactoring-examples.md`** — Infrastructure refactoring walkthrough, prohibited architectural patterns
- **`references/page-state-pattern.md`** — UI page state implementation patterns with 3 levels (data bag, async Facade-calling, polling/concurrency), registration, and wiring examples

### Examples

- **`examples/anti-pattern-god-page.razor`** — Anti-pattern: God Page with 20+ fields, polling, concurrency in `@code` block
