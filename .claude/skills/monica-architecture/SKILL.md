---
name: monica-architecture
description: This skill should be used when the user asks to "design module structure", "plan module architecture", "review module layout", "create new module", "refactor module structure", "module folder structure", "module boundaries", "facade pattern", "internal vs public", "feature-first", "annotations folder", "developer-facing attributes", "where to put attributes", "page decomposition", "page too large", "extract page state", "IWebModule", "web module", "downgrade to non-web", "模块架构", "架构设计", "模块结构", "文件夹结构", or needs guidance on Monica module directory layout, layer responsibilities, dependency direction, public/internal boundaries, Facade placement, Provider separation, Annotations placement, page decomposition rules, module runtime kind selection, Features pattern for bundled sub-modules, or Mixed/Standalone/Composite UI module patterns.
version: 1.1.4
---

# Monica Unified Module Architecture

This skill defines the canonical architecture for all Monica modules. It is the single source of truth for module structure decisions.

Other skills reference this skill:
- `monica-development` — for module registration, Res type, service patterns
- `monica-ui-development` — for UI component, page, and styling patterns

## Quick Decision Guide

**Creating a new module?** → Use the Infrastructure Module Template below.
**Adding UI to an existing module?** → Choose Mixed (lightweight UI) or Standalone (complex UI).
**Module getting large?** → Use the Features Pattern when a real sub-domain boundary emerges. File count is only a secondary signal.
**Need developer-facing attributes?** → Place in `Annotations/` (public layer).
**Need telemetry metrics?** → Place metric-specific files in `Metrics/`; use `$monica-opentelemetry` for implementation rules.
**Page file getting large?** → Check the Page Decomposition Rules.
**Unsure where a file goes?** → Check the Standard Layer Names table.
**Unsure if something is public or internal?** → Check the Visibility Rules table.
**Working in `Modules/`?** → See `Modules/ Is Registration Only`.
**Grouping related files?** → Prefer prefix naming. Introduce sub-folders only when a folder stops being scannable. See Folder Depth & Grouping Rules.
**Folder depth reaching 4 levels?** → Treat it as a design smell and justify it explicitly.
**Need ASP.NET Core middleware or endpoints?** → Use the Web vs Non-Web Module Kind rule below. Do not assume every UI module is a web module.

## Core Principles

### 1. Prefer Simple Root Layers First

Start with simple root layers first, then switch to feature folders when a real sub-domain boundary emerges.

When features become the dominant unit, use **Feature Folder + Layer-Inside-Feature** — it prevents any single root layer from becoming a dumping ground.
Feature folder names should usually stay aligned with the module or sub-module name, following the `Monica.DevOps/` style such as `Git/`, `K8S/`, and `FileOps/`, unless a special requirement suggests a different name.

### Localization Placement Exception

Localization resources are a project-level concern in Monica. Even when the rest of a module uses feature folders, keep resource marker classes and JSON files under the project root `Localization/` folder, not under feature subfolders. This keeps the resource namespace, embedded resource path, and Monica localization validation workflow consistent.

### 2. Facades Are the Host-Facing Entry Point for API and UI

Every module exposes host-facing use cases through `Facades/`:

- Return `Res` / `Res<T>` (unified response model)
- Consumed by Minimal API endpoints, UI components, and host integration code directly
- Delegate to internal `Services/` for implementation
- Are NOT cross-module contracts; other modules depend on `Abstractions/` + public `Models/`
- Must NOT contain substantial business logic

Facades live in the **infrastructure module**, not the UI module:

```
Minimal API / Host Code ──→ Facade (Res<T>) ──→ Services (internal)
UI Component             ──→ Facade (Res<T>) ──→ Services (internal)
Other Module             ──→ Abstractions + Models (public) ←── Services implement
```

### 3. Public vs Internal Boundary

**Public surface**:
- `Modules/` — module registration entry points
- `Abstractions/` — interfaces for cross-module dependency
- `Annotations/` — developer-facing attributes for declarative configuration
- `Models/` — shared data contracts
- `Facades/` — host-facing `Res<T>` entry points for API/UI/host code
- `Metrics/` — metric name constants when hosts need to subscribe to a module meter
- `Events/` — public domain or integration events
- `Exceptions/` — module-specific public exceptions when part of the contract
- `Extensions/` — only when intentionally exposed as integration helpers

**Internal implementation** (hidden inside the module):
- `Abstractions/Internal/` — internal-only contracts
- `Models/Internal/` — internal-only data types
- `Services/` — all implementation logic
- `Metrics/` — metric instruments, app-owned metric state, and instrumentation adapters by default
- `Providers/` — pluggable strategy implementations

The `Internal/` sub-folder convention makes this boundary visible in the directory structure.

### 4. Dependency Direction

Allowed:
```
UI Page / Minimal API / Host Code → Facade (Res<T>) → Services → Providers
Other Module → Abstractions (interfaces) + Models (public)
```

Forbidden:
- Other Module → Facade
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

### 7. Choose ModuleBase vs WebModuleBase Explicitly

Use the runtime kind that matches the module lifecycle:

- `ModuleBase<T...>` is the default. Use it when the module only needs builder, service-registration, post-service, and dependency phases.
- `WebModuleBase<T...>` is only for modules that actually participate in `ConfigureApplicationBuilder` or `ConfigureEndpoints`.
- A UI project does not imply `IWebModule`. If a UI module only registers pages, dialogs, localized components, shell items, or state/support types, keep it on `ModuleBase`.
- If a web-capable module still has meaningful non-web behavior, implement downgrade explicitly with `CanDowngradeToNonWebModule()`. In downgrade mode the non-web phases still run, while web pipeline and endpoint phases are skipped.
- When exposing module-system diagnostics or dashboards, surface capability separately from runtime mode. `IsWebModule` and `IsDowngradedFromWebModule` answer different questions and should not be collapsed into one flag.

### 8. Utils Is the Unified Utility Folder

`Utils/` replaces `Helpers/`, `Tools/`, `Utilities/`, `Common/`, `Misc/`. Only one utility folder name is allowed.

Exception: `Tools/` is reserved for AI tool providers in Monica.AI modules.

## Folder Depth & Grouping Rules

### Prefer Shallow Trees

Prefer to stay within **3 levels** from project root: `Feature/Layer/SubLayer/`. A 4th level is a design smell and should be introduced only with a clear reason.

```
✅ Authorization/Services/Support/PolicyRequirement.cs        (3 levels)
❌ Authorization/Services/Support/Policies/PolicyRequirement.cs (4 levels — usually a smell)
```

### Prefix Naming Over Sub-Folders

**Prefer prefix naming to group related files** within a folder instead of creating sub-folders. This leverages IDE alphabetical sorting to achieve visual grouping without folder overhead.

This is the standard pattern used by ASP.NET Core, EF Core, and MudBlazor:
```
# ASP.NET Core — Authentication/ has many files, zero sub-folders
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
├── PermissionBitCheckerRegistry.cs
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

Use these as heuristics, not hard thresholds:

- Prefer flat folders with prefix naming by default.
- Introduce a sub-folder when a folder stops being scannable in the IDE.
- Avoid creating a sub-folder that only holds one or two files unless it marks a real boundary such as `Internal/`.
- Treat a 4th nesting level as a smell; prefer renaming or regrouping before adding depth.
- Keep `Internal/` as a visibility boundary.
- Keep `Providers/{ProviderName}/` when a provider is a real replaceable unit.

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
| `Metrics/` | .NET metric names, instruments, app-owned metric state, and instrumentation adapters | Mixed |
| `Providers/` | Pluggable strategy implementations | Private |
| `Modules/` | Module registration units | Public |
| `Extensions/` | Extension methods | Depends on usage |
| `Events/` | Domain/integration events | Public |
| `Exceptions/` | Module-specific exception types | Public |
| `Utils/` | Pure utility functions | Private |

Group files within a layer using **prefix naming** by default. Introduce sub-folders only when that improves scanability (see Folder Depth & Grouping Rules above).

### Metrics Layer Rules

Use `Metrics/` for module telemetry names, metric owner services, app-owned metric state, and instrumentation adapters. Keep dashboard cards, profiling pages, and UI metric displays in the UI folders. For OpenTelemetry-compatible implementation rules, instrument choices, registration, and tag policy, use `$monica-opentelemetry`.

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
├── Metrics/                             # (optional, telemetry metrics; see $monica-opentelemetry)
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

Use **prefix naming** to group related support files (e.g., `PolicyRequirement.cs`, `PolicyHandler.cs`, `PolicyProvider.cs`). Introduce sub-folders within `Support/` only when flat naming stops being scannable.

## Features Pattern (Bundled Sub-Modules)

**When to use**: When a real sub-domain boundary emerges, especially if the module is accumulating multiple independent use-case areas. File count is a signal, not the rule.

```
Monica.{Name}/
├── Modules/
│   ├── Module{Name}.cs                  # Consolidated root module registration file
│   └── Module{SubFeature}.cs            # Consolidated sub-feature registration file
├── {FeatureA}/                          # OR under Features/
│   ├── Abstractions/
│   ├── Models/
│   ├── Facades/
│   ├── Metrics/
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
- Prefer `UI{Name}/` feature directories for mixed or composite UI modules. Standalone single-feature UI modules may use the root-level equivalent layout.
- Most UI modules stay on `ModuleBase`. Only use `WebModuleBase` for UI modules that truly configure middleware or endpoints.

### Standalone UI Module

```
Monica.{Name}.UI/
├── Modules/
│   └── Module{Name}UI.cs                # Consolidated UI module registration file
├── Pages/
│   ├── UI{Name}{Action}Page.razor       # {Action} optional when there is only one primary page
│   └── UI{Name}{Action}Page.razor.css
├── Components/
├── Dialogs/                         # (optional)
├── Models/                          # View-only (minimize — reuse Facade Models)
├── State/                           # Browser/page/session state
└── Support/                         # Resolvers, formatters, coordinators
├── Localization/
└── wwwroot/                             # (optional)
```

In a standalone single-feature UI module, root-level `Components/`, `Dialogs/`, `Models/`, `State/`, and `Support/` are the single-feature equivalents of `UI{Name}/...`.

### Composite UI Module

When one UI project hosts multiple sub-modules:

```
Monica.{Family}.UI/
├── Modules/
│   ├── Module{FeatureA}UI.cs            # Consolidated registration file for FeatureA UI
│   ├── Module{FeatureB}UI.cs            # Consolidated registration file for FeatureB UI
│   └── Module{Family}UI.cs              # (optional, aggregator) consolidated registration file
├── Pages/
│   ├── UI{FeatureA}{Action}Page.razor
│   └── UI{FeatureB}{Action}Page.razor
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
│   └── UI{Name}{Action}Page.razor
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
| Infrastructure adapter | `{Strategy}{Capability}{Role}` | `OpenAIProvider`, `FileDocumentIndexStateStore`, `RedisStateStoreClient` |

Avoid vague catch-all names such as `Manager`, `Helper`, and `Core`. `Handler` is acceptable when it matches a framework or pipeline concept.

### UI Naming

| Component | Pattern | Example |
|-----------|---------|---------|
| UI module | `Module{Name}UI` | `ModuleRAGUI` |
| UI folder | `UI{Name}/` | `UIRAG/` |
| Page | `UI{Name}[Action]Page` | `UIRAGManagePage`, `UIK8SPage` |
| Route URL | `/{name}-{action}` | `/rag-manage` |

## Visibility Rules

| Layer | Default | Rationale |
|-------|---------|-----------|
| `Abstractions/` | `public` | External modules depend on these |
| `Abstractions/Internal/` | `internal` | Module-internal contracts |
| `Annotations/` | `public` | Developer-facing declarative attributes |
| `Models/` | `public` | Shared data contracts |
| `Models/Internal/` | `internal` | Module-internal data types |
| `Facades/` | `public` | Host-facing API + UI entry points |
| `Services/` | `internal` | Implementation details |
| `Providers/` | `internal` | Pluggable but internal |
| `Modules/` | `public` | Module registration entry points |
| `Events/` | `public` | Public events when part of the module contract |
| `Exceptions/` | `public` | Public exceptions when part of the module contract |
| `Extensions/` | depends | Public only when intentionally exposed |
| `Utils/` | `internal` | Module-internal utilities |

## Additional Resources

### Reference Files

- **`references/refactoring-examples.md`** — Infrastructure refactoring walkthrough, prohibited architectural patterns
- **`references/page-state-pattern.md`** — UI page state implementation patterns with 3 levels (data bag, async Facade-calling, polling/concurrency), registration, and wiring examples

### Examples

- **`examples/anti-pattern-god-page.razor`** — Anti-pattern: God Page with 20+ fields, polling, concurrency in `@code` block
