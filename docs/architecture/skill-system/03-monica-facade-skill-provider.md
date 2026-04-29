# Doc 03 — Monica Facade AI Skill Provider

> **Status.** Design proposal. Phase C of the implementation roadmap.
> **Audience.** Framework team and every Monica module owner who ships a Facade.
> **Depends on.** Doc 02 (`MoSkill<TSelf>`, `[MoAITool]`, description-priority chain).
> **Last revised.** 2026-04-29.

## 0. Why this doc exists

Every Monica module that exposes capabilities to host code does so through a Facade — `Monica.AI.RAG.Facades.RAGFacade`, the `KnowledgeBaseFacade` introduced by Doc 01, `Monica.JobScheduler.Facades.JobSchedulerFacade`, and so on. Today these Facades are invisible to the chat agent. Anyone who wants to expose a Facade method as an agent-callable capability has to write an `IAIChatToolProvider` by hand, marshal each method through `AIFunctionFactory.Create`, and register the provider via `services.TryAddEnumerable(...)`. The single existing example (`KnowledgeSearchToolProvider`, 426 lines for four tools) shows how much friction that has.

This doc specifies a generalized mechanism: a new module `ModuleAIFacadeProvider` in `Monica.Framework` (which already references `Monica.AI`) that auto-projects every Monica module's Facade methods into one module-level Skill. Each selected Facade contributes scripts to that module Skill, and the loaded skill content groups those scripts by Facade. Authors get auto-discovery; descriptions come from XML doc comments by default; opt-out is one attribute on a method.

The rule is opt-out, not opt-in: if your module loads `ModuleAIFacadeProvider`, every public method on every Facade marked `IMonicaFacade` is exposed as a script unless you explicitly disable it. This matches the "default-include" ergonomics the user asked for.

## 1. Premise and module placement

### 1.1 Project location

`Monica.Framework/Monica.Framework.csproj` already contains `<ProjectReference Include="..\Monica.AI\Monica.AI.csproj" />`. So the new module sits in `Monica.Framework`:

```
Monica.Framework/
  AISkillProviders/
    Facade/
      Modules/
        ModuleAIFacadeProvider.cs
        ModuleAIFacadeProviderGuide.cs
        ModuleAIFacadeProviderOption.cs
      Internal/
        ModuleFacadeSkill.cs        // AgentClassSkill subclass per Monica module
        FacadeScriptGroup.cs        // groups generated scripts by Facade for skill content
        FacadeMethodScanner.cs      // reflection + filtering pipeline
        FacadeScriptFactory.cs      // builds AgentSkillScript via CreateScript
```

Module key: add `BuiltInModuleKey.AIFacadeProvider` (next to `AI`) in `Monica.Core/Modularity/Models/BuiltInModuleKey.cs`.

The naming convention for the module class is `ModuleAIFacadeProvider` — chosen over `ModuleMonicaFacadeAIProvider` and `ModuleFacadeAISkillProvider` because:

- `Monica` prefix is implicit in any module name.
- `AI` first means "this serves the AI surface" (matches `ModuleAI`, `ModuleAIUI`).
- `FacadeProvider` describes function: it provides Skills derived from Facades.

### 1.2 Dependency claims

```csharp
public override void ClaimDependencies()
{
    DependsOnModule<ModuleAIGuide>().Register();
    DependsOnModule<ModuleSkillSystemGuide>().Register();
    DependsOnModule<ModuleXmlDocumentationGuide>().Register();
}
```

The Skill System (Doc 02) hosts the discovery pipeline; the Facade Provider piggybacks on it. XML documentation is required so the description-priority chain has its bottom rung.

## 2. Module Facade Skill — one per Monica module

### 2.1 Construction approach

The Provider does **not** rely on Microsoft's CRTP-attribute-based script discovery. Facade methods carry no `[MoAITool]` markers in the general case (the Provider's default is opt-out, not opt-in), so attribute-driven discovery would not find them. Instead, the Provider reflects selected Facades, creates `AgentSkillScript` instances for their public methods, groups those scripts by Facade for the loaded skill content, and registers **one** `AgentSkill` per owning Monica module.

This matches the Microsoft Agent Framework shape: `AgentSkillsProvider` advertises a flat list of skills, then exposes the generic `load_skill` and `run_skill_script` tools. There is no nested skill activation level, so Doc 03 does not create per-Facade `AgentSkill`s.

A single internal skill class:

```csharp
namespace Monica.Framework.AISkillProviders.Facade.Internal;

internal sealed class ModuleFacadeSkill(
    ModuleKey moduleKey,
    AgentSkillFrontmatter frontmatter,
    string instructions,
    IReadOnlyList<FacadeScriptGroup> facadeGroups,
    IReadOnlyList<AgentSkillScript> scripts)
    : MoSkill<ModuleFacadeSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = frontmatter;
    protected override string Instructions { get; } = instructions;

    /// <summary>
    /// Pre-built script list supplied by the Facade Provider's reflection pass.
    /// The override bypasses MoSkill's [MoAITool] discovery because Facade methods
    /// are opt-out by default and do not require [MoAITool] as a marker.
    /// </summary>
    public override IReadOnlyList<AgentSkillScript>? Scripts { get; } = scripts;

    public ModuleKey ModuleKey { get; } = moduleKey;
    public IReadOnlyList<FacadeScriptGroup> FacadeGroups { get; } = facadeGroups;
}

internal sealed record FacadeScriptGroup(
    Type FacadeType,
    string FacadeName,
    string Description,
    IReadOnlyList<AgentSkillScript> Scripts);
```

`ModuleFacadeSkill` inherits `MoSkill<ModuleFacadeSkill>` for consistency with hand-written Skills (Doc 02 §2). The override of `Scripts` short-circuits both `MoSkill<TSelf>`'s `[MoAITool]` discovery and Microsoft's `[AgentSkillScript]` discovery — the constructor-supplied list wins outright.

`MoSkill<TSelf>`'s `RequiredModules`, `IsEnabled`, and `Priority` virtual hooks remain available; the Provider sets them to defaults (no required modules — implicit gating handled by §7.2; enabled; priority 0).

### 2.2 Frontmatter

The generated skill frontmatter represents the owning Monica module, not an individual Facade.

- **Name.** `module-{moduleKey-kebab}`. Examples: `module-rag`, `module-knowledge-base`, `module-job-scheduler`.
- **Description.** Resolved through:
  1. Module class XML `<summary>`.
  2. Fallback: `"The {ModuleKey} module."`

The kebab-case conversion is straightforward but specific:

| Source | Kebab |
|---|---|
| `RAG` | `rag` |
| `KnowledgeBase` | `knowledge-base` |
| `JobScheduler` | `job-scheduler` |

Implementation: split on the boundary between a lowercase / digit followed by an uppercase, lowercase the result, hyphenate. Acronyms (RAG, AI) keep their letters but join via hyphen.

### 2.3 Loaded skill content

The `Instructions` string becomes the facade-grouped catalog shown after the agent calls `load_skill`. Template:

```text
This module exposes scripts grouped by Facade:

{FacadeName}: {facade-description}
- {script-name-1}: {script-description-1}
- {script-name-2}: {script-description-2}

{NextFacadeName}: {facade-description}
- {script-name-3}: {script-description-3}

Use run_skill_script with skillName "{module-skill-name}", the exact scriptName,
and the script arguments described in the script schema.
```

Facade descriptions are resolved through:

1. `[Description("...")]` on the Facade type.
2. `IXmlDocumentationService.GetTypeDocumentation(facadeType)` — the type's XML `<summary>`.
3. Fallback: `"{ModuleKey}.{FacadeName}"`, e.g., `"Monica.AI.RAG.RAGFacade"`.

`[MoAITool]` is not allowed on classes by Doc 02 §5.1, so it is not part of type-description resolution.

### 2.4 Method-to-script projection

For each public instance method on each selected Facade in the module:

1. Apply the **deny-list filter** (§2.5).
2. Apply the **`[MoAITool(Disabled = true)]` filter** — drop the method if disabled.
3. Resolve the script **name**: `[MoAITool(Name = "...")]` if present (Doc 02 §5.1); otherwise `{facade-prefix}-{method-kebab}`. The Facade prefix is the Facade type name with trailing `Facade` stripped and kebab-cased. Example: `KnowledgeBaseFacade.CreateAsync` → `knowledge-base-create`.
4. Resolve the script **description** through the priority chain in Doc 02 §5.2.
5. Resolve each parameter **description** through the priority chain in Doc 02 §5.2.
6. Build an `AgentSkillScript` via `AgentClassSkill<ModuleFacadeSkill>.CreateScript(name, methodDelegate, description)`.
7. The method delegate is bound to a per-call-resolved Facade instance (§7.1), with an `IServiceProvider` parameter injected for scoped DI.

Result: one `AgentSkillScript` per surviving public Facade method. The flattened script list goes into `ModuleFacadeSkill.Scripts`; the same scripts are also retained in `FacadeScriptGroup` entries so `Instructions` can present them under their owning Facade.

Name collisions inside one module skill are forbidden. The discovery pipeline detects duplicate script names before registration and throws at startup with a clear message identifying both source methods. Authors fix the collision by setting `[MoAITool(Name = "...")]` explicitly on one or both methods.

### 2.5 Default deny-list

These methods are silently dropped. Listed by signature, not just name:

| Pattern | Reason |
|---|---|
| `Dispose()`, `DisposeAsync()` | `IDisposable` / `IAsyncDisposable` plumbing. |
| `ToString()`, `GetHashCode()`, `Equals(object)` | `System.Object` overrides. |
| `GetType()` | `System.Object` member. |
| `MemberwiseClone()` | `System.Object` member. |
| Any method with at least one `out` or `ref` parameter | Cannot be JSON-serialized by `AIFunctionFactory`. |
| Any method returning `IQueryable<T>`, `IQueryable`, `Task<IQueryable<T>>` | Streaming query types are not JSON-serializable; they are server-side query builders. |
| Any method with `[CompilerGenerated]` attribute | Anonymous types, lambda closures, etc. |
| Property accessors (get/set) — implicit because the scanner only walks `MethodInfo` flagged `IsSpecialName == false` | Properties are not exposed as scripts. |
| `op_*` operator methods | Not callable as agent tools. |
| Generic methods (`MethodInfo.IsGenericMethodDefinition == true`) | Cannot bind a generic without a concrete type argument; the agent has no way to provide one. |

Methods returning `Res` or `Res<T>` are **not** in the deny-list; they go through the `Res` unwrap rule in §8.

### 2.6 Worked example — `RAGFacade.GetKnowledgeBasesAsync`

Source method:

```csharp
/// <summary>Returns all knowledge bases visible to the current user.</summary>
public Task<Res<IReadOnlyList<KnowledgeBase>>> GetKnowledgeBasesAsync()
{
    // ...
}
```

After projection:

| Property | Value |
|---|---|
| Module skill name | `module-rag` |
| Script name | `rag-get-knowledge-bases` |
| Script description | `"Returns all knowledge bases visible to the current user."` (XML `<summary>`, priority 3) |
| Parameters | `[]` (no parameters; the `IServiceProvider` is implicit) |
| Return type | `string` (the JSON-serialized `List<KnowledgeBase>` extracted from the `Res<>` envelope per §8) |

The agent does not see `rag-get-knowledge-bases` as a top-level tool. It first loads the module skill, then invokes the script through Agent Framework's generic `run_skill_script` tool:

```json
{
  "skillName": "module-rag",
  "scriptName": "rag-get-knowledge-bases",
  "arguments": {}
}
```

The script schema appears inside the loaded `module-rag` skill content, grouped under `RAGFacade`.

### 2.7 Worked example — `KnowledgeBaseFacade.CreateAsync`

Source method (with explicit `[MoAITool]`):

```csharp
[MoAITool(Description = "Create a knowledge base.")]
public Task<Res<KnowledgeBase>> CreateAsync(
    [MoAITool(Description = "Stable id, kebab-case, e.g. 'company-handbook'.")]
    string id,
    [MoAITool(Description = "Display name shown to users.")]
    string name,
    [MoAITool(Description = "Optional human-readable description.")]
    string? description = null)
{
    // ...
}
```

After projection:

| Property | Value |
|---|---|
| Module skill name | `module-knowledge-base` |
| Script name | `knowledge-base-create` |
| Script description | `"Create a knowledge base."` (priority 1, `[MoAITool]`) |
| Parameter `id` | `"Stable id, kebab-case, e.g. 'company-handbook'."` |
| Parameter `name` | `"Display name shown to users."` |
| Parameter `description` | `"Optional human-readable description."` (optional, default `null`) |

The agent invokes it through `run_skill_script`:

```json
{
  "skillName": "module-knowledge-base",
  "scriptName": "knowledge-base-create",
  "arguments": {
    "id": "company-handbook",
    "name": "Company Handbook",
    "description": "Internal handbook knowledge base."
  }
}
```

The script schema is emitted in the loaded `module-knowledge-base` skill content, grouped under `KnowledgeBaseFacade`.

### 2.8 Worked example — disabled method

Source:

```csharp
[MoAITool(Disabled = true)]
public Task<Res> AdminPurgeAsync()
{
    // ...
}
```

After projection: **not surfaced**. The method is excluded from the script list before `CreateScript` is called. XML docs and `[Description]` annotations on the method are ignored.

## 3. Future-work slot — hand-written module Skills

This rev does not implement, but explicitly reserves space for, hand-written `MoSkill<TSelf>` subclasses that belong to a module without being Facade-derived. A module could register a `RAGAdvancedSearchSkill : MoSkill<RAGAdvancedSearchSkill>` (e.g., a hand-curated multi-step retrieval pattern). A future iteration may list those child skills in the `ModuleFacadeSkill` content next to the Facade script groups.

This is explicitly out of scope. The doc declares the slot so future iterations don't re-litigate it.

## 4. Facade discovery contract

### 4.1 The marker

```csharp
namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Marker interface implemented by every Monica module Facade that should be
/// auto-projected as an AI Skill by ModuleAIFacadeProvider.
/// </summary>
public interface IMonicaFacade
{
}
```

The interface is defined in `Monica.AI/Skills/Abstractions/IMonicaFacade.cs` (Doc 02 reserves the home folder; Doc 03 names the interface). Every existing Facade that wants opt-in adds `: IMonicaFacade` to its declaration. This is a one-liner per Facade and an explicit, grep-able opt-in:

```csharp
// Before:
public class RAGFacade(...)

// After:
public class RAGFacade(...) : IMonicaFacade
```

The doc-writer in Phase C is responsible for adding the marker to every existing Facade across `Monica.AI`, `Monica.Framework`, `Monica.JobScheduler`, etc. — a mechanical pass.

### 4.2 Why a marker, not a naming convention

Two alternatives were considered and rejected:

- **Filter on type name `*Facade`.** Brittle: catches helper classes named `*Facade` that aren't really Facades. Doesn't survive renames.
- **Filter on namespace `*Facades`.** Same problem; depends on developer convention.

The marker interface is cheap to test (`type.IsAssignableTo(typeof(IMonicaFacade))`), invisible at runtime cost, and explicit. It also gives module owners a clear "I don't want this exposed" path: just don't add the marker.

### 4.3 Discovery pipeline

`ModuleAIFacadeProvider.IterateBusinessTypes`:

```csharp
public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
{
    foreach (var type in types)
    {
        if (type is { IsClass: true, IsAbstract: false }
            && type.IsAssignableTo(typeof(IMonicaFacade)))
        {
            _discoveredFacades.Add(type);
        }
        yield return type;
    }
}
```

Collected types are processed in `PostConfigureServices`:

1. Filter by `Option.RegistrationMode` (§5).
2. Group by owning module (the module that has the Facade in its assembly's `ModuleBase`-rooted graph). The grouping uses `[ModuleKey]`-attributed types in the same assembly as the heuristic.
3. For each surviving Facade, scan eligible public methods and build `AgentSkillScript`s (§2.4).
4. For each module that contributed at least one script, build one `ModuleFacadeSkill` (§2) with its scripts grouped by Facade in `FacadeScriptGroup` entries.
5. Register each `ModuleFacadeSkill` as a singleton `AgentSkill` service. The Skill System hosted service from Doc 02 §6.2 picks them up and adds them to the `AgentSkillsProvider`.

## 5. Selective registration — guide methods

### 5.1 The two guide methods

`ModuleAIFacadeProviderGuide` exposes:

```csharp
public sealed class ModuleAIFacadeProviderGuide
    : ModuleGuide<ModuleAIFacadeProvider, ModuleAIFacadeProviderOption, ModuleAIFacadeProviderGuide>
{
    private const string CONFIG_REGISTRATION = nameof(CONFIG_REGISTRATION);
    protected override string[] GetRequestedConfigMethodKeys() => [CONFIG_REGISTRATION];

    /// <summary>
    /// Register every IMonicaFacade-marked type discovered in loaded assemblies.
    /// </summary>
    public ModuleAIFacadeProviderGuide UseAllFacades()
    {
        ConfigureModuleOption(option =>
        {
            option.RegistrationMode = FacadeRegistrationMode.All;
            option.AllowedFacadeTypes = null;
        });
        ConfigureEmpty(CONFIG_REGISTRATION);
        return this;
    }

    /// <summary>
    /// Register only the listed Facade types. Each type must implement IMonicaFacade.
    /// </summary>
    public ModuleAIFacadeProviderGuide UseFacades(params Type[] facadeTypes)
    {
        ArgumentNullException.ThrowIfNull(facadeTypes);
        foreach (var type in facadeTypes)
        {
            if (!type.IsAssignableTo(typeof(IMonicaFacade)))
            {
                throw new ArgumentException(
                    $"Type '{type.FullName}' does not implement IMonicaFacade.",
                    nameof(facadeTypes));
            }
        }
        ConfigureModuleOption(option =>
        {
            option.RegistrationMode = FacadeRegistrationMode.Selective;
            option.AllowedFacadeTypes = facadeTypes.ToHashSet();
        });
        ConfigureEmpty(CONFIG_REGISTRATION);
        return this;
    }
}
```

The `Mo` extension methods:

```csharp
public static class ModuleAIFacadeProviderBuilderExtensions
{
    extension(Mo)
    {
        public static ModuleAIFacadeProviderGuide AddAIFacadeSkills(
            Action<ModuleAIFacadeProviderOption>? action = null)
        {
            return new ModuleAIFacadeProviderGuide().Register(action);
        }
    }
}
```

Usage:

```csharp
// Register everything.
Mo.AddAIFacadeSkills().UseAllFacades();

// Register selectively.
Mo.AddAIFacadeSkills().UseFacades(
    typeof(RAGFacade),
    typeof(KnowledgeBaseFacade));
```

`UseAllFacades()` and `UseFacades(...)` are mutually exclusive — calling both throws because `CONFIG_REGISTRATION` is asserted twice. This is Monica's standard "exactly one configuration must be picked" pattern.

### 5.2 Per-method exclusion

There is no guide-level allowlist or filter delegate. Per-method exclusion lives entirely in code via `[MoAITool(Disabled = true)]`. Confirmed by user during plan refinement.

```csharp
public class RAGFacade : IMonicaFacade
{
    public Task<Res<List<KnowledgeBase>>> GetAllAsync()  // exposed
    { /* ... */ }

    [MoAITool(Disabled = true)]                             // not exposed
    public Task<Res> DangerousAdminAsync()
    { /* ... */ }
}
```

This trades configurability for simplicity: the Facade author owns the surface decision, the host operator just opts in or out at the type level.

### 5.3 Module Skill behavior under selective mode

Under `UseFacades(typeof(RAGFacade))`, the Module Skill `module-rag` is built with only `RAGFacade` scripts — even though `KnowledgeBaseFacade` might also belong to the same associated module. The loaded skill content reflects what's actually registered, not the full theoretical set.

This is intentional: the module skill's job is to advertise *available* scripts, not capabilities the host has chosen to suppress.

## 6. `[MoAITool]` attribute interaction

Doc 02 §5 defines the attribute. Doc 03's role is to specify how it interacts with auto-projection:

| Annotation | Effect |
|---|---|
| `[MoAITool]` (no properties) on a method | Description still resolves via the priority chain (priority 1 is empty, falls through). The method is still surfaced. The annotation alone is a no-op. |
| `[MoAITool(Description = "...")]` on a method | Description override (priority 1 wins). Method surfaced. |
| `[MoAITool(Disabled = true)]` on a method | Method excluded. Description annotations (XML, `[Description]`) are ignored. |
| `[MoAITool(Description = "...")]` on a parameter | Parameter description override. Method exposure unchanged. |
| `[MoAITool(Disabled = true)]` on a parameter | `Disabled` is ignored on parameters per Doc 02 §5.1. Logged at registration as a warning so authors notice the misuse. |
| `[Description("...")]` on a method | Priority 2 in the chain. Used when `[MoAITool]` is absent. |
| `[Description("...")]` on a parameter | Priority 2 in the chain. |
| XML `<summary>` on a method | Priority 3 in the chain. |
| XML `<param name="...">` on a parameter | Priority 3 in the chain. |
| Nothing | Description = `"{TypeName}.{MethodName}"`, warning logged. |

### 6.1 Worked example combining all rungs

```csharp
public class HypotheticalFacade : IMonicaFacade
{
    /// <summary>List items.</summary>
    public Task<Res<List<Item>>> ListAsync() => /* ... */;
    // → script "hypothetical-list", description "List items." (XML, priority 3)

    [Description("Get one item.")]
    public Task<Res<Item>> GetAsync(string id) => /* ... */;
    // → script "hypothetical-get", description "Get one item." ([Description], priority 2)

    [MoAITool(Description = "Create a new item.")]
    public Task<Res<Item>> CreateAsync(
        [Description("XML doc says 'Item id'; this overrides.")]
        [MoAITool(Description = "Stable item id.")]
        string id) => /* ... */;
    // → script "hypothetical-create", description "Create a new item." (priority 1)
    //   parameter id description "Stable item id." (priority 1, beats [Description])

    [MoAITool(Disabled = true)]
    public Task<Res> InternalAdminAsync() => /* ... */;
    // → not surfaced.
}
```

## 7. Lifecycle

### 7.1 Per-call Facade resolution

A `ModuleFacadeSkill` is a singleton, but each underlying Facade instance must be resolved per script invocation so that scoped services injected into the Facade work correctly. The script delegate built in §2.4 step 6 captures the Facade *type* (not instance) and resolves the instance from `IServiceProvider` on each call:

```csharp
internal static class FacadeScriptFactory
{
    internal static Delegate BuildScriptDelegate(
        Type facadeType, MethodInfo facadeMethod)
    {
        // The synthesized delegate signature mirrors facadeMethod's parameters,
        // plus an injected IServiceProvider for resolving the Facade per call.
        // Implementation uses System.Linq.Expressions to build a strongly-typed
        // delegate that AIFunctionFactory.Create can introspect.
        // ...
    }
}
```

The `IServiceProvider` parameter is treated specially by `AIFunctionFactory.Create` — it's not surfaced in the JSON schema, it's resolved by the framework at invocation time. Microsoft documents this pattern for `[AgentSkillScript]`-annotated methods; the Facade Provider reuses it.

### 7.2 Module gating

A `ModuleFacadeSkill`'s `RequiredModules`-equivalent gate is implicit: the discovery scan only sees Facade types whose owning assemblies are loaded. There is no per-Skill `RequiredModules` because `IMonicaFacade` types aren't `MoSkill<TSelf>` subclasses. If a Facade lives in an assembly that isn't loaded, the type isn't discovered, and no script is created for it.

The `ModuleFacadeSkill` is built only for modules with at least one surviving Facade script. If `Mo.AddRAG()` is not called, no `RAGFacade` is registered (today, RAG facades are registered conditionally inside `ModuleRAG`), and `module-rag` doesn't appear.

### 7.3 Async vs sync

`IBusinessTypeIterator.IterateBusinessTypes` is synchronous. Facade discovery happens synchronously at iteration phase. The hosted service that builds the `AgentSkillsProvider` (Doc 02 §6.2) runs at startup; it iterates the registered `AgentSkill` services synchronously and builds the provider in one go. There is no per-session work — match Doc 02's lifecycle rule.

## 8. `Res` and `Res<T>` unwrap semantics

### 8.1 Why this matters

Most Facade methods return `Res` or `Res<T>` — Monica's standard envelope for "did this succeed, and if so what's the payload, and if not what's the error message." The agent doesn't care about the envelope; it wants the success payload, or a clear error.

### 8.2 The rule

For each script generated from a Facade method:

| Method return type | Script return | Failure handling |
|---|---|---|
| `Task<Res>` | `Task<string>` returning `"OK"` (or `Res.Message` when present) | `IsFailed` → throw `MoAIToolFailureException(res.Message)` |
| `Task<Res<T>>` | `Task<string>` returning JSON-serialized `T` | `IsFailed` → throw `MoAIToolFailureException(res.Message)` |
| `Res` (sync) | `string` returning `"OK"` (or `Res.Message`) | Same as `Task<Res>` |
| `Res<T>` (sync) | `string` returning JSON-serialized `T` | Same as `Task<Res<T>>` |
| `T` directly (no `Res` wrapper) | `Task<string>` / `string` returning JSON-serialized `T` | No special handling; ordinary exceptions bubble. |
| `void` / `Task` | `Task<string>` returning `"OK"` | No payload; ordinary exceptions bubble. |

Microsoft's Agent Framework reports thrown exceptions on a script as a tool error to the agent — the framework catches, formats, and surfaces the error message in the tool-call result. So throwing `MoAIToolFailureException(res.Message)` cleanly maps a `Res.Fail("Knowledge base not found.")` into a tool error message the agent can read.

### 8.3 The exception type

```csharp
namespace Monica.AI.Skills.Abstractions;

/// <summary>
/// Thrown when a Skill script wraps a Monica Res result that failed.
/// Microsoft's Agent Framework surfaces the message as a tool-call error
/// to the agent. The agent treats it as an actionable error string,
/// not an unhandled exception.
/// </summary>
public sealed class MoAIToolFailureException(string message) : Exception(message)
{
}
```

Logging on throw is the framework's responsibility (the SkillSystem hosted service installs an `IAIChatAgentDecorator` that catches `MoAIToolFailureException` once per script call and logs the failure with structured telemetry — out of scope for this doc, see Doc 02 §11(c) Phase E note on telemetry).

### 8.4 Why not return `Res` JSON directly to the agent

It tempted the design. We rejected it because the agent has no idea what `IsSucceed` / `Data` / `Message` are; it interprets the JSON schema literally. Forcing the agent to inspect a wrapper envelope on every call adds friction and cognitive load. Throwing an exception when the Res failed is the framework-idiomatic path.

## 9. Module options

```csharp
public sealed class ModuleAIFacadeProviderOption
    : ModuleOptions<ModuleAIFacadeProvider>
{
    /// <summary>
    /// Whether to register all discovered Facades (UseAllFacades) or only the
    /// allowlisted set (UseFacades). Set by the Guide; not user-set directly.
    /// </summary>
    public FacadeRegistrationMode RegistrationMode { get; set; }
        = FacadeRegistrationMode.None;

    /// <summary>
    /// When RegistrationMode == Selective, only Facade types in this set are
    /// registered. Null when RegistrationMode == All.
    /// </summary>
    public IReadOnlySet<Type>? AllowedFacadeTypes { get; set; }

    /// <summary>
    /// Maximum DTO recursion depth for parameter-schema generation. Default: 3.
    /// Prevents pathological cycles on highly nested types.
    /// </summary>
    public int MaxParameterSchemaDepth { get; set; } = 3;

    /// <summary>
    /// Whether to log a warning when a method's description falls back to the
    /// "{TypeName}.{MethodName}" placeholder. Default: true.
    /// </summary>
    public bool WarnOnMissingDescription { get; set; } = true;
}

public enum FacadeRegistrationMode
{
    None,
    All,
    Selective
}
```

## 10. Open questions and resolutions

| # | Question | Resolution |
|---|---|---|
| (a) | Default deny-list completeness. | **Defined in §2.5.** The set is exhaustive for v1: `IDisposable` plumbing, `Object` overrides, `ref`/`out` params, `IQueryable` returns, generics, operators, special-name members. Future additions are non-breaking. |
| (b) | Complex DTO parameter handling — recursion depth. | **Recursive XML-doc descent with depth limit 3.** Implemented via `ModuleAIFacadeProviderOption.MaxParameterSchemaDepth`. Beyond depth 3, the parameter is described as `{TypeName}` with no nested doc — agents see the JSON schema name only. Cycles are detected and pruned. |
| (c) | Per-method permission gates. | **Out of scope this rev.** `[MoAITool]` reserves `RequiredPermissions` as a documented forward-compat slot per Doc 02 §5.1; the property is not shipped until a future security doc owns it. |
| (d) | What about Facades that aren't yet `IMonicaFacade`-marked? | **Mechanical migration.** Phase C's PR adds the marker to every existing Facade. No Facade is silently exposed without the marker. The PR is reviewable as a checklist (one line per Facade). |
| (e) | Method-name collisions inside a module Skill (e.g., overloads or explicit name overrides). | **Forbidden.** Two methods that produce the same script name inside one `ModuleFacadeSkill` are rejected at startup with a clear message. Authors fix the collision by renaming one method or by setting `[MoAITool(Name = "...")]` explicitly on one of them per Doc 02 §5.1. |
| (f) | What if the Facade's owning module key isn't on `BuiltInModuleKey`? | **Use `(ModuleKey)moduleKeyString`.** Third-party modules with custom `ModuleKey`s work transparently — the kebab-case and frontmatter generation use the `ModuleKey.Value` string. |

## 11. Acceptance criteria for Phase C

When Phase C is implemented:

1. `Monica.Framework/AISkillProviders/Facade/` folder exists with all files listed in §1.1.
2. `BuiltInModuleKey.AIFacadeProvider` exists.
3. `Mo.AddAIFacadeSkills().UseAllFacades()` and `Mo.AddAIFacadeSkills().UseFacades(...)` are callable from a host `Program.cs`.
4. Every existing Monica Facade has been marked `: IMonicaFacade`. The migration PR includes a comprehensive checklist of Facades touched.
5. End-to-end smoke test: a host registers `Mo.AddAI()`, `Mo.AddRAG()`, `Mo.AddKnowledgeBase()`, and `Mo.AddAIFacadeSkills().UseAllFacades()`. The chat agent's system prompt contains entries for `module-rag`, `module-knowledge-base`, and `module-ai`. Loading each module skill shows Facade-grouped scripts with method-derived names and non-fallback descriptions.
6. The smoke test verifies the Agent Framework call shape: Facade methods are invoked through `run_skill_script` using `skillName`, `scriptName`, and `arguments`; individual scripts are not top-level tools.
7. The `Res<T>` unwrap rule is verified in the smoke test: a script that calls `RAGFacade.SearchAsync` returns the unwrapped `IReadOnlyList<TextSearchResult>` JSON. A script that calls a method that returns `Res.Fail("...")` results in a tool error with the failure message visible to the agent.
8. The two worked examples from §2.6 and §2.7 are reproduced verbatim in the integration test fixtures.
9. Solution builds with **zero new warnings**.

## 12. Cross-doc references

- Doc 01 (`01-knowledge-base-decoupling.md`) — defines `KnowledgeBaseFacade`, the canonical worked example used in §2.7.
- Doc 02 (`02-skill-tool-mcp-base.md`) — defines `MoSkill<TSelf>`, `[MoAITool]`, the description-priority chain. Doc 03 normatively references Doc 02 §5.1, §5.2, §6.
- Doc 04 (`04-projectunit-skill-provider.md`) — mirrors Doc 03 for ProjectUnit ApplicationServices. Doc 04 normatively references Doc 03 for the attribute interaction (§6), `Res<T>` unwrap rule (§8), and selective-registration pattern (§5).

---

**End of Doc 03.**
