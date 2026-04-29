# Doc 02 — Skill / Tool / MCP Base Classes + `[MoAITool]` Bridge

> **Status.** Design proposal. Spine of the Monica AI skill-system refactor.
> **Audience.** Framework team. Foundation that Docs 03 and 04 normatively reference.
> **Last revised.** 2026-04-29.

## 0. Why this doc exists

Today, exposing a capability to a Monica chat agent requires writing an `IAIChatToolProvider` implementation, hand-rolling `Microsoft.Extensions.AI.AIFunctionFactory.Create(...)` calls inside `ConfigureAsync`, and registering the provider with `services.TryAddEnumerable(...)` in a module's `ConfigureServices`. The single existing example, `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs`, weighs in at 426 lines for four tools. There is no class-based authoring path, no MCP integration, and no automatic mapping from XML doc comments to tool descriptions.

This doc specifies the three base classes that fix that — `MoSkill<TSelf>`, `MoTool`, and `MoMcp` — together with a single `[MoAITool]` attribute that supplies description and disable metadata, and a deterministic priority chain for resolving descriptions from `[MoAITool]`, `[Description]`, or XML doc comments.

Docs 03 (Facade Skill Provider) and 04 (ProjectUnit Skill Provider) build directly on the contracts defined here. Doc 01 (Knowledge Base decoupling) names the two worked examples (`KnowledgeBaseLookupSkill`, `RAGKnowledgeSkill`) that this doc uses.

## 1. Reference patterns this design anchors on

### 1.1 `Microsoft.Agents.AI.AgentClassSkill<TSelf>`

The Microsoft Agent Framework (package `Microsoft.Agents.AI` v1.3.0, already referenced in `Monica.AI.csproj`) provides an abstract base class `AgentClassSkill<TSelf>` for defining skills as ordinary C# classes. Source-of-truth XML at `/mnt/c/Users/mo/.nuget/packages/microsoft.agents.ai/1.3.0/lib/net10.0/Microsoft.Agents.AI.xml`. Key members:

- `abstract AgentSkillFrontmatter Frontmatter { get; }` — kebab-case name + description; the L1 discovery layer surfaced in the agent's system prompt.
- `protected abstract string Instructions { get; }` — full instructions appended on activation.
- `virtual IReadOnlyList<AgentSkillResource>? Resources { get; }` — defaults to discovering members marked `[AgentSkillResource("name")]` on `TSelf`.
- `virtual IReadOnlyList<AgentSkillScript>? Scripts { get; }` — defaults to discovering methods marked `[AgentSkillScript("name")]` on `TSelf`.
- Helper factories: `CreateScript(string name, Delegate method, string? description, JsonSerializerOptions?)` and two `CreateResource(...)` overloads for the *explicit-override* path.

`TSelf` is annotated with `[DynamicallyAccessedMembers]`, so the CRTP pattern keeps the design AOT-compatible and trim-friendly. Methods and members with an `IServiceProvider` parameter receive scoped DI on each invocation. Scripts marshal parameters and return values via `Microsoft.Extensions.AI.AIFunctionFactory`.

The progressive-disclosure model has two levels:
- **L1 — discovery.** Frontmatter is advertised in the system prompt; the agent decides whether the skill is relevant.
- **L2 — activation.** Full Instructions, Resources, and Scripts only flow into context once the skill is selected.

This is exactly the "lazy discovery" requirement Monica needs. The Monica design therefore inherits from `AgentClassSkill<TSelf>` directly rather than building a parallel binding API.

### 1.2 Monica's `IBusinessTypeIterator`

`Monica.Core/Modularity/Abstractions/IBusinessTypeIterator.cs`:

```csharp
public interface IBusinessTypeIterator
{
    IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types);
}
```

Modules opt in by implementing this interface. Monica's `ModuleRegistry` runs the iteration phase **after `ConfigureServices` and before `PostConfigureServices`**, feeding it the type set produced by `Mo.Options.GlobalTypeFinder.GetTypes()`. Each iterator inspects each type, builds its own metadata, and `yield return`s the type so downstream iterators see the same stream.

Canonical consumer: `Monica.JobScheduler/Modules/ModuleJobScheduler.cs` (lines 54–77), which filters for `IRecurringJob` / `ITriggeredJob<T>`, reads `[JobConfig]`, builds `JobDefinition`, registers the type as transient, and forwards definitions to `JobRegistrationHostedService`. Doc 02 reuses this exact shape.

### 1.3 `IXmlDocumentationService`

`Monica.Framework/XmlDocumentation/Abstractions/IXmlDocumentationService.cs`:

```csharp
public interface IXmlDocumentationService
{
    XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method);
    string? GetTypeDocumentation(Type type);
    void ClearCache();
    IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments();
}
```

`XmlMethodDocumentation` exposes the method `<summary>` plus a per-parameter dictionary keyed on parameter name. Module: `Monica.Framework/Modules/ModuleXmlDocumentation.cs`. The skill-system depends on this module being loaded; the Skill discovery host registers a hard dependency on `ModuleXmlDocumentationGuide`.

### 1.4 `ModuleKey`

`Monica.Core/Modularity/Models/ModuleKey.cs` is a `readonly record struct` with implicit conversions from `BuiltInModuleKey` (the enum at `Monica.Core/Modularity/Models/BuiltInModuleKey.cs`) and from `string`:

```csharp
public override IEnumerable<ModuleKey> RequiredModules =>
    [BuiltInModuleKey.RAG, (ModuleKey)"Vendor.Custom"];
```

The skill-system uses `ModuleKey`, not `string`, for any "this Skill / Tool / MCP needs module X loaded" predicate.

## 2. `MoSkill<TSelf>` — the Skill base

### 2.1 Class signature and inheritance

```csharp
namespace Monica.AI.Skills.Abstractions;

public abstract class MoSkill<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties)] TSelf>
    : AgentClassSkill<TSelf>
    where TSelf : MoSkill<TSelf>
{
    /// <summary>
    /// Module keys this skill requires. The skill is silently skipped at registration
    /// when any required module is not loaded. Default: empty (no module gate).
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Hard-disable switch evaluated at the iterator phase. Default: true.
    /// Override to disable conditionally (e.g., based on environment).
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Ordering hint used when multiple skills overlap. Higher runs earlier.
    /// Default: 0.
    /// </summary>
    public virtual int Priority => 0;

    /// <summary>
    /// Override of <see cref="AgentClassSkill{TSelf}.Scripts"/> that uses Monica's
    /// <see cref="MoAIToolAttribute"/> as the script-discovery marker instead of
    /// Microsoft's <c>[AgentSkillScript]</c>. Methods on <typeparamref name="TSelf"/>
    /// annotated with <c>[MoAITool]</c> become scripts; the script name comes from
    /// <c>[MoAITool(Name = ...)]</c> when present, otherwise from the method name
    /// converted to kebab-case (with the <c>Async</c> suffix stripped).
    /// </summary>
    /// <remarks>
    /// Subclasses that need explicit script construction (e.g., the Provider-built
    /// Facade and ProjectUnit Skills in Docs 03 and 04) re-override this property
    /// to return a manually-built list. The Facade / ProjectUnit Providers do exactly
    /// that — they pass a pre-built script collection to a subclass constructor and
    /// the override returns it directly.
    /// </remarks>
    public override IReadOnlyList<AgentSkillScript>? Scripts =>
        _moDiscoveredScripts ??= MoSkillScriptDiscovery.Discover<TSelf>(this);

    private IReadOnlyList<AgentSkillScript>? _moDiscoveredScripts;
}
```

Direct inheritance from `AgentClassSkill<TSelf>` means hand-written Monica skills inherit Microsoft's L1/L2 discovery model. `[AgentSkillResource]` continues to work as Microsoft documents — Monica does not override resource discovery in this rev. `IServiceProvider` parameters give per-invocation scoped DI; CRTP propagates the `[DynamicallyAccessedMembers]` constraint for AOT.

For **scripts**, Monica overrides `Scripts` so `[MoAITool]` is the single discovery marker. Authors do **not** write `[AgentSkillScript("name")]` on Monica skill methods — that ceremony is replaced by `[MoAITool(Name = "name")]` (or just `[MoAITool]` for an auto-kebab-case name). One attribute, not two. `[MoAITool(Disabled = true)]` is honored as an exclusion gate even when Name is provided.

There is **no** Monica class-level attribute. All metadata flows from overridden members, from `AgentSkillFrontmatter`, and from per-method `[MoAITool]`.

### 2.2 Required overrides on a concrete Skill

A concrete subclass overrides:

- `AgentSkillFrontmatter Frontmatter` — kebab-case name and short description. Names must be lowercase letters, numbers, and hyphens only; no leading, trailing, or consecutive hyphens (the `AgentSkillFrontmatter` constructor enforces this).
- `string Instructions` — the full skill prompt that flows into context on activation. Multi-paragraph allowed.

A concrete subclass *may* override:

- `IEnumerable<ModuleKey> RequiredModules` — module gate.
- `bool IsEnabled` — hard disable.
- `int Priority` — ordering hint.
- `IReadOnlyList<AgentSkillResource>? Resources` and `IReadOnlyList<AgentSkillScript>? Scripts` — only if the attribute-based path doesn't fit (e.g., dynamic per-instance script generation).

### 2.3 Worked example — migrating `KnowledgeSearchToolProvider`

The existing 426-line `KnowledgeSearchToolProvider` becomes a `MoSkill<TSelf>` subclass. Skeleton:

```csharp
namespace Monica.AI.RAG.Skills;

public sealed class RAGKnowledgeSkill(
    KnowledgeToolService knowledgeToolService,
    RAGService ragService,
    IOptions<ModuleRAGOption> ragOptions,
    ILogger<RAGKnowledgeSkill> logger)
    : MoSkill<RAGKnowledgeSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        name: "rag-knowledge",
        description: "Retrieve grounded facts and citations from indexed knowledge bases.");

    protected override string Instructions =>
        "Use these scripts together when the user asks about content in the selected " +
        "knowledge bases. Rewrite the question into a focused retrieval query before " +
        "calling search-knowledge-base. Use browse-knowledge-documents to discover " +
        "candidate documents, browse-knowledge-document-tree to navigate folder " +
        "hierarchy, and get-knowledge-document-content to load full source text. " +
        "Always cite the source name and source link.";

    public override IEnumerable<ModuleKey> RequiredModules => [BuiltInModuleKey.RAG];

    [MoAITool(
        Name = "search-knowledge-base",
        Description =
            "Semantic search over the selected knowledge bases. Returns ranked excerpts " +
            "with source name and source link for citation.")]
    public async Task<string> SearchAsync(
        [MoAITool(Description = "Focused retrieval query, not conversational text.")]
        string query,
        [MoAITool(Description = "Optional original user question for context.")]
        string? userQuestion = null,
        [MoAITool(Description = "Top-K result count override.")]
        int? topK = null,
        CancellationToken ct = default)
    {
        var payload = await knowledgeToolService.SearchKnowledgeAsync(
            "search-knowledge-base", _knowledgeBases, query, userQuestion, topK, ct);
        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    // ... three more scripts: browse-knowledge-documents, browse-knowledge-document-tree,
    //                         get-knowledge-document-content.
}
```

Notes on the migration:

- The four tool methods are now first-class C# methods on the skill class, marked with a single `[MoAITool(Name = "...", Description = "...")]`.
- If `Name` is omitted, Monica derives `search` from `SearchAsync` (kebab-case + drop `Async` suffix). Authors who want a longer name (e.g., `search-knowledge-base`) supply it explicitly.
- Per-session knowledge-base ID list (today resolved through `AIChatAgentCreateContext.KnowledgeBaseIds`) flows through an `IServiceProvider`-scoped service that exposes the active KB list, *not* through script parameters. Scripts stay user-facing-clean.
- Hard module gate via `RequiredModules`: the skill is silently skipped when `Monica.AI.RAG` is not loaded. The Knowledge Base lookup-only Skill (Doc 01) requires `[BuiltInModuleKey.KnowledgeBase]`.

### 2.4 Lifecycle

- **Skill instance — singleton.** The discovery host registers each `MoSkill<TSelf>` subclass as a singleton in DI. `Frontmatter`, `Instructions`, `Resources`, and `Scripts` collections are evaluated once and cached.
- **Script invocation — scoped.** Microsoft's `[AgentSkillScript]` machinery resolves an `IServiceProvider` parameter (when present) by creating a per-invocation scope. Scoped dependencies (DbContext, UnitOfWork, ChatSessionContext) come through that scope.
- **No per-session skill rebuild.** The earlier rev 1 design proposed rebuilding the skill collection each session; rev 2 replaces that with Microsoft's progressive disclosure (frontmatter-only at L1, content on activation). This is simpler and matches the framework's intended use.

## 3. `MoTool` — standalone class-based tool

### 3.1 When to use it

A `MoTool` is for capabilities that aren't naturally bundled inside a Skill. Examples: a single utility function the agent always has access to (current time, FX rate lookup), or a tool emitted by a non-skill module that wants to expose one method without authoring a full skill.

If the capability has more than ~3 closely related methods or needs shared instructions, prefer `MoSkill<TSelf>`. Skills group naturally and progressively disclose; lone tools always sit in the system prompt.

### 3.2 Base class

```csharp
namespace Monica.AI.Skills.Abstractions;

public abstract class MoTool : AITool
{
    /// <summary>
    /// Tool name advertised to the agent. Required; lowercase letters, digits,
    /// and hyphens only.
    /// </summary>
    public abstract override string Name { get; }

    /// <summary>
    /// Tool description. Defaults to the description-priority chain on the
    /// <see cref="InvokeAsync"/> method (see §5).
    /// </summary>
    public override string Description => MoAIDescriptionResolver.Resolve(GetType().GetMethod(nameof(InvokeAsync)));

    /// <summary>
    /// Module keys this tool requires. The tool is silently skipped at
    /// registration when any required module is not loaded.
    /// </summary>
    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    /// <summary>
    /// Hard disable. Default: true.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// The tool body. Parameter descriptions are resolved through the
    /// description-priority chain (see §5). Implementations may declare any
    /// signature accepted by Microsoft.Extensions.AI.AIFunctionFactory.Create
    /// (primitives, strings, [Description]'d DTOs, IServiceProvider for DI,
    /// CancellationToken).
    /// </summary>
    public abstract Delegate InvokeAsync { get; }
}
```

Implementation pattern:

```csharp
public sealed class CurrentTimeTool : MoTool
{
    public override string Name => "current-time";

    public override Delegate InvokeAsync => GetTime;

    [MoAITool(Description = "Returns the server's current UTC time in ISO 8601.")]
    private static string GetTime() => DateTimeOffset.UtcNow.ToString("O");
}
```

The discovery host wires the `InvokeAsync` delegate through `AIFunctionFactory.Create` so the tool is a fully-formed `Microsoft.Extensions.AI.AITool` registered with the active agent's tool collection.

### 3.3 Lifecycle

`MoTool` instances are singletons. If a tool needs scoped state, declare an `IServiceProvider` parameter on the `InvokeAsync` delegate and resolve scoped services on each call (same pattern as `[AgentSkillScript]`).

## 4. `MoMcp` — class-based MCP service

### 4.1 Status: verify-before-implement

The Microsoft Agent Framework's MCP integration package (likely `Microsoft.Agents.AI.Mcp` or `ModelContextProtocol.Server`) was not present in the local NuGet cache when this doc was authored. The exact base type Monica wraps and the exact API surface depend on which package the Monica.AI module ends up referencing. **The doc-writer of the implementation phase must verify the package version, decompile / read its public surface, and finalize the `MoMcp` API at that time.**

### 4.2 Intended shape (placeholder)

The intent mirrors `MoTool`:

```csharp
namespace Monica.AI.Skills.Abstractions;

public abstract class MoMcp : /* Microsoft MCP server base, TBD */
{
    public abstract string ServiceName { get; }

    public virtual IEnumerable<ModuleKey> RequiredModules => [];

    public virtual bool IsEnabled => true;

    // MCP-specific surface — determined when the package is selected.
}
```

Discovery uses the same `IBusinessTypeIterator` flow as `MoSkill<TSelf>` and `MoTool`. The `[MoAITool]` description bridge applies uniformly to whatever method-shaped surfaces the chosen MCP package exposes.

### 4.3 Implementation note

Implementation of `MoMcp` is **deferred** until after Phase B of the implementation sequencing (see §11). The `MoSkill<TSelf>` and `MoTool` work is independent and ships first. The discovery host described in §6 reserves an iterator branch for `MoMcp` so the contract is forward-compatible.

## 5. `[MoAITool]` attribute and the description priority chain

### 5.1 Attribute spec

```csharp
namespace Monica.AI.Skills.Annotations;

[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Parameter,
    AllowMultiple = false,
    Inherited = false)]
public sealed class MoAIToolAttribute : Attribute
{
    /// <summary>
    /// Script / tool name override. When omitted, Monica derives the name from
    /// the member name: kebab-case, with a trailing <c>Async</c> stripped.
    /// On parameters this property is ignored.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description override. Highest priority in the description chain.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When true, the method's tool / script is excluded from registration.
    /// On a parameter this property is ignored — disable the whole method
    /// instead, or omit the parameter from the surface.
    /// </summary>
    public bool Disabled { get; set; }
}
```

Usage by target:

| Target | Effect |
|---|---|
| Method on a `MoSkill<TSelf>` (hand-written skill) | **Discovery marker** — without `[MoAITool]` the method is not exposed as a script. `Name` overrides the auto-derived script name; `Description` flows through the priority chain; `Disabled = true` excludes. Microsoft's `[AgentSkillScript]` is no longer used on Monica skill methods. |
| Method on a Facade (Doc 03) | Additive metadata. The Facade Provider's default is opt-out, so the method is exposed even without `[MoAITool]`. `Name` overrides the auto-derived script name. `Description` overrides the priority chain. `Disabled = true` excludes. |
| Method on an `ApplicationService` (Doc 04) | Same as Facade. Mutating CRUD methods require an explicit `[MoAITool]` (any properties — even just `[MoAITool]`) to be exposed at all (Doc 04 §4). |
| Method on a `MoTool.InvokeAsync` delegate target | Description override; `Name` ignored (the tool name comes from `MoTool.Name`). |
| Parameter | Description override for the parameter's JSON schema entry. `Name` and `Disabled` are ignored. |
| Class | **Not allowed.** Compile-time error via `AttributeUsage`. |

The forward-compatibility slot for `string[] RequiredPermissions` is documented here for posterity but **not** added to the shipped attribute in this rev. A future security doc owns it.

#### Name auto-derivation

When `[MoAITool]` is present without `Name`, the script / tool name is derived as:

1. Take the method name (e.g., `SearchKnowledgeBaseAsync`).
2. Strip a trailing `Async` if present → `SearchKnowledgeBase`.
3. Kebab-case via the same rule used in Doc 03 §2.2 → `search-knowledge-base`.

The derivation is fully deterministic. If two methods on the same skill produce the same auto-derived name, registration throws at startup with a clear message identifying both methods. Authors fix the collision by setting `Name` explicitly on one or both.

### 5.2 Description priority chain

For a method's description, the resolver walks:

1. `[MoAITool(Description = ...)]` on the method.
2. `[System.ComponentModel.Description("...")]` on the method.
3. `IXmlDocumentationService.GetMethodDocumentation(method)?.Summary`.
4. (Fallback) `null`. The discovery host emits a placeholder `"{TypeName}.{MethodName}"` and logs a warning at registration time. Tooling-friendly so missing docs are visible without breaking the build.

For a parameter's description, the resolver walks:

1. `[MoAITool(Description = ...)]` on the parameter.
2. `[System.ComponentModel.Description("...")]` on the parameter.
3. `IXmlDocumentationService.GetMethodDocumentation(method)?.Parameters[parameter.Name]`.
4. (Fallback) `null` → empty string in the JSON schema.

The rule is identical for `MoSkill<TSelf>` scripts, `MoTool` invocations, and Provider-generated Facade / ProjectUnit scripts. Authoring is uniform: write XML doc comments by default, use `[MoAITool]` only when the script-facing description must differ from the developer-facing XML.

A central helper performs the resolution:

```csharp
namespace Monica.AI.Skills.Internal;

internal static class MoAIDescriptionResolver
{
    internal static string ResolveMethod(MethodInfo method, IXmlDocumentationService xmlDocs)
    {
        var fromAttr = method.GetCustomAttribute<MoAIToolAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromAttr)) return fromAttr;

        var fromDescription = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromDescription)) return fromDescription;

        var fromXml = xmlDocs.GetMethodDocumentation(method)?.Summary;
        if (!string.IsNullOrWhiteSpace(fromXml)) return fromXml;

        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    internal static string ResolveParameter(
        ParameterInfo parameter, IXmlDocumentationService xmlDocs)
    {
        var fromAttr = parameter.GetCustomAttribute<MoAIToolAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromAttr)) return fromAttr;

        var fromDescription = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(fromDescription)) return fromDescription;

        var methodDocs = xmlDocs.GetMethodDocumentation(parameter.Member as MethodInfo
            ?? throw new InvalidOperationException("Parameter must belong to a method."));
        if (parameter.Name is { } name
            && methodDocs?.Parameters.TryGetValue(name, out var fromXml) == true
            && !string.IsNullOrWhiteSpace(fromXml))
        {
            return fromXml;
        }

        return string.Empty;
    }
}
```

### 5.3 Worked examples

Three methods on a hypothetical `RAGFacade`:

```csharp
public class RAGFacade : IMonicaFacade
{
    /// <summary>Returns all knowledge bases visible to the current user.</summary>
    public Task<Res<List<KnowledgeBase>>> GetKnowledgeBasesAsync() { /* ... */ }
    // → description = "Returns all knowledge bases visible to the current user."
    //   (XML-only path, priority 3)

    [MoAITool(Description = "Reindex a knowledge base. Long-running.")]
    public Task<Res> ReindexAsync(
        [MoAITool(Description = "KB id, e.g., 'company-handbook'.")] string id)
    { /* ... */ }
    // → method description from [MoAITool] (priority 1)
    //   parameter description from [MoAITool] (priority 1)

    [MoAITool(Disabled = true)]
    public Task<Res> AdminPurgeAsync() { /* ... */ }
    // → not registered as a script; description is irrelevant.
}
```

A parameter that has both an `[MoAITool]` and an XML `<param>` doc resolves to the `[MoAITool]` description (priority 1 wins over priority 3). The XML doc is preserved for human readers.

## 6. Discovery — `ModuleSkillSystem`

### 6.1 Module placement

A new module `ModuleSkillSystem` lives in `Monica.AI`. The module:

- Implements `IBusinessTypeIterator`.
- Owns the discovery + filtering pipeline for `MoSkill<TSelf>`, `MoTool`, and `MoMcp` subclasses.
- Builds the `AgentSkillsProvider` at startup via `AgentSkillsProviderBuilder.UseSkills(...)`.
- Adds a hard dependency on `ModuleXmlDocumentationGuide` (via `ClaimDependencies`) so the description chain has its bottom rung available.

Provisional declaration:

```csharp
[ModuleKey((ModuleKey)"AISkillSystem")]   // not yet a BuiltInModuleKey
public sealed class ModuleSkillSystem(ModuleSkillSystemOption option)
    : ModuleBase<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>(option),
      IBusinessTypeIterator
{
    private readonly List<Type> _skillTypes = [];
    private readonly List<Type> _toolTypes = [];
    private readonly List<Type> _mcpTypes = [];   // forward-compat slot

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false })
            {
                if (IsAssignableToOpenGeneric(type, typeof(MoSkill<>)))
                {
                    _skillTypes.Add(type);
                }
                else if (type.IsAssignableTo(typeof(MoTool)))
                {
                    _toolTypes.Add(type);
                }
                else if (type.IsAssignableTo(typeof(MoMcp)))
                {
                    _mcpTypes.Add(type);
                }
            }

            yield return type;
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        foreach (var skillType in _skillTypes)
        {
            services.AddSingleton(skillType);
            services.AddSingleton(typeof(AgentSkill), sp => sp.GetRequiredService(skillType));
        }
        foreach (var toolType in _toolTypes)
        {
            services.AddSingleton(toolType);
            services.AddSingleton(typeof(AITool), sp => sp.GetRequiredService(toolType));
        }
        // MCP registration deferred (see §4.3).

        services.AddSingleton<IAgentSkillsProviderFactory, MonicaAgentSkillsProviderFactory>();
        services.AddHostedService<MonicaSkillsProviderHostedService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAIGuide>().Register();
        DependsOnModule<ModuleXmlDocumentationGuide>().Register();
    }
}
```

Helper `IsAssignableToOpenGeneric` walks the inheritance chain to detect `MoSkill<>` regardless of `TSelf` parameterization. Implementation reuses `Monica.Tool.Extensions` patterns (already imported by `ModuleJobScheduler`).

### 6.2 The `MonicaSkillsProviderHostedService`

A hosted service activated at startup:

1. Resolves all registered `AgentSkill` services.
2. Filters by `IsEnabled` and `RequiredModules` — for each `MoSkill<TSelf>` instance, drop it when any required module is not in the loaded module set (the loaded set is available through `Mo.Options` or the module registry).
3. Calls `new AgentSkillsProviderBuilder().UseSkills(filtered).Build()` and registers the result as a singleton `AgentSkillsProvider` for the chat agent factory to consume.

Same flow for `AITool` (active tools list passed to the agent builder via `AIChatAgentBuilder.AddTool` — see §8).

### 6.3 Registration entrypoint (Guide)

```csharp
public sealed class ModuleSkillSystemGuide
    : ModuleGuide<ModuleSkillSystem, ModuleSkillSystemOption, ModuleSkillSystemGuide>
{
    protected override string[] GetRequestedConfigMethodKeys() => [];
}

public static class ModuleSkillSystemBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables Monica's class-based AI skill / tool / MCP discovery and registration.
        /// Discovers all <see cref="MoSkill{TSelf}"/>, <see cref="MoTool"/>, and
        /// <see cref="MoMcp"/> subclasses via <see cref="IBusinessTypeIterator"/>,
        /// builds a Microsoft.Agents.AI.AgentSkillsProvider, and exposes the
        /// resulting capability set to the chat agent.
        /// </summary>
        public static ModuleSkillSystemGuide AddAISkillSystem(
            Action<ModuleSkillSystemOption>? action = null)
        {
            return new ModuleSkillSystemGuide().Register(action);
        }
    }
}
```

The Facade Provider (Doc 03) and ProjectUnit Provider (Doc 04) modules call `DependsOnModule<ModuleSkillSystemGuide>().Register()` in their `ClaimDependencies` override.

## 7. Lifecycle and DI reconciliation

| Concept | Lifetime | Notes |
|---|---|---|
| `MoSkill<TSelf>` instance | Singleton | Frontmatter, Instructions, RequiredModules, IsEnabled evaluated once. |
| `[MoAITool]` script invocation | Per-call (scoped via `IServiceProvider` parameter) | Same `CreateScript`-backed pipeline Microsoft uses for `[AgentSkillScript]`; Monica's discovery just chooses the marker. |
| `MoTool` instance | Singleton | Same as Skill. |
| `MoTool.InvokeAsync` invocation | Per-call (scoped via `IServiceProvider` parameter) | Wired through `AIFunctionFactory.Create`. |
| `MoMcp` | TBD when package is finalized | Reserve singleton + per-call scope as the default. |
| `AgentSkillsProvider` | Singleton, built once at startup | Skill set is immutable per process. Hot-reload is out of scope. |

Module gate evaluation is **iterator-phase only**. Skills whose `RequiredModules` aren't satisfied are dropped before any `AgentSkillsProvider` is built. A skill cannot become enabled mid-process by loading a new module dynamically — Monica modules are loaded at startup and that's the moment the gate fires.

## 8. Migrating the existing `IAIChatToolProvider` surface

### 8.1 Migration target: `KnowledgeSearchToolProvider`

The existing provider becomes `Monica.AI.RAG.Skills.RAGKnowledgeSkill : MoSkill<RAGKnowledgeSkill>` (skeleton in §2.3). Migration steps, all in a single PR:

1. Move the four nested local methods (`SearchKnowledgeAsync`, `BrowseKnowledgeDocumentsAsync`, `BrowseKnowledgeDocumentTreeAsync`, `GetKnowledgeDocumentContentAsync`) up to instance methods on the new skill class.
2. Annotate each with `[MoAITool(Name = "kebab-name", Description = "...")]`. No `[AgentSkillScript]`.
3. Remove the manual `KnowledgeBase[]` plumbing — wire through a scoped `IKnowledgeBaseSelectionAccessor` exposed via `IServiceProvider` script parameter.
4. Remove `services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIChatToolProvider, KnowledgeSearchToolProvider>())` from `ModuleRAG.ConfigureServices`. Discovery handles registration.
5. Delete `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs`.

### 8.2 Breaking change — `IAIChatToolProvider` is removed

`Monica.AI/Abstractions/IAIChatToolProvider.cs` is **deleted** in this PR. There is no back-compat shim, no `[Obsolete]` adapter, no migration grace period. Per the Monica development-stage policy in `CLAUDE.md` ("Backward compatibility is not a concern unless explicitly instructed otherwise"), the simpler architecture wins.

The only existing consumer of `IAIChatToolProvider` is `KnowledgeSearchToolProvider`, which §8.1 migrates atomically. Any out-of-tree consumer must migrate to `MoSkill<TSelf>` in the same release.

### 8.3 `AIChatAgentBuilder` interaction

`Monica.AI/Services/Support/AIChatAgentBuilder.cs` is simplified. The `AddTool(AITool)` accumulator is removed from the public surface — tools come from the singleton `AgentSkillsProvider` produced by `MonicaSkillsProviderHostedService`. The builder forwards the `AgentSkillsProvider` to the underlying `Microsoft.Agents.AI.AIAgent` build call. Standalone `MoTool` instances are added through the same provider, not through ad-hoc `AddTool` calls.

If `AIChatAgentBuilder` was consumed externally for `AddTool` use cases, those consumers also migrate to `MoSkill<TSelf>` or `MoTool` subclasses in the same release.

## 9. Microsoft Agent Framework binding

Concrete pinning, per `Monica.AI.csproj`:

```
Microsoft.Agents.AI                        1.3.0
Microsoft.Extensions.AI                    10.5.0
Microsoft.Extensions.AI.Abstractions       10.5.0
Microsoft.Extensions.AI.OpenAI             10.5.0
Microsoft.Extensions.VectorData.Abstractions  10.5.0
```

Used surfaces:

- `Microsoft.Agents.AI`: `AgentSkill`, `AgentClassSkill<TSelf>`, `AgentSkillFrontmatter`, `[AgentSkillResource]`, `AgentSkillsProvider`, `AgentSkillsProviderBuilder`, `AgentSkillResource`, `AgentSkillScript`, the `CreateScript(...)` / `CreateResource(...)` factories on `AgentClassSkill<TSelf>`. Note: `[AgentSkillScript]` is **not** used by Monica; `[MoAITool]` replaces it as the script-discovery marker (§5.1).
- `Microsoft.Extensions.AI`: `AIFunctionFactory.Create`, `AITool`, `AIFunction`, `IChatClient`, `ChatOptions`.

MCP package selection (see §4.1) is **explicitly deferred** until Phase B of implementation. No package is pinned in this doc.

## 10. Worked-example summary

The doc-writer of the implementation phase must produce one end-to-end migration example as part of the eventual code review:

| Before | After |
|---|---|
| `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs` (426 lines) | `Monica.AI/RAG/Skills/RAGKnowledgeSkill.cs` (target ~200 lines, four methods) |
| `services.TryAddEnumerable(...)` in `ModuleRAG.ConfigureServices` | nothing — auto-discovery |
| Tool descriptions hand-rolled in `BuildKnowledgeSearchToolDescription` | XML doc + `[MoAITool(Description = ...)]` |
| Per-session tool collection wired through `AIChatAgentBuilder.AddTool` | `AgentSkillsProvider` built once at startup |

## 11. Open questions and resolutions

| # | Question | Resolution |
|---|---|---|
| (a) | Evaluate `IsEnabled` / `RequiredModules` per session, or only at iterator phase? | **Iterator phase only.** Cheap, predictable, no surprise gating mid-conversation. |
| (b) | Add a Monica-specific class-level marker attribute (`[MoSkill]` parameterless)? | **No.** CRTP via `: MoSkill<TSelf>` already gives the marker. |
| (c) | Where do the three base classes physically live? | **`Monica.AI/Skills/Abstractions/`** (new `Skills` feature folder under `Monica.AI`). The annotations live in `Monica.AI/Skills/Annotations/`. The discovery module lives in `Monica.AI/Skills/Modules/`. |
| (d) | Multi-level inheritance (e.g., `SpecialSkill : RAGKnowledgeSkill`) — does discovery still work? | **No.** Microsoft's CRTP discovery reflects only on `TSelf`. Each leaf must re-apply CRTP: `class SpecialSkill : MoSkill<SpecialSkill>`. The doc cites Microsoft's documented limitation. |
| (e) | MCP package selection. | **Verify-before-implement.** Doc 02 reserves the slot; the implementation phase pins the package after inspecting Microsoft's MCP integration release at that time. |
| (f) | Forward-compat `[MoAITool(RequiredPermissions = ...)]` slot. | **Not in this rev.** A future security doc owns it. The attribute does not ship the property; adding it later is a non-breaking change because attribute properties are additive. |

## 12. Acceptance criteria for implementation

When Phase B (this doc) is implemented, the following must hold:

1. `Monica.AI/Skills/Abstractions/MoSkill.cs`, `MoTool.cs` exist. `MoMcp.cs` is a placeholder file with a `// TODO: package selection` comment.
2. `Monica.AI/Skills/Annotations/MoAIToolAttribute.cs` exists with `Name`, `Description`, and `Disabled` properties; `AttributeUsage` constrained to method + parameter.
3. `Monica.AI/Skills/Internal/MoSkillScriptDiscovery.cs` exists and implements the `[MoAITool]`-marker-based reflection used by `MoSkill<TSelf>.Scripts`. The auto-derived kebab-case name rule is unit-tested.
4. `Monica.AI/Skills/Modules/ModuleSkillSystem.cs` implements `IBusinessTypeIterator` and registers a hosted service that builds `AgentSkillsProvider`.
5. `Monica.AI.RAG.Skills.RAGKnowledgeSkill` migrated from `KnowledgeSearchToolProvider`. `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs` deleted. `services.TryAddEnumerable(...)` registration removed.
6. `Monica.AI/Abstractions/IAIChatToolProvider.cs` **deleted**. No shim, no `[Obsolete]` adapter. `Monica.AI/Services/Support/AIChatAgentBuilder.cs` no longer exposes `AddTool(AITool)`.
7. The `IXmlDocumentationService.GetMethodDocumentation` path is exercised by at least one method per skill in tests (compile-time verification: every Facade-method-style script has a description in one of the three sources).
8. `ModuleRAG` no longer registers `KnowledgeSearchToolProvider`; the new `RAGKnowledgeSkill` is auto-discovered.
9. Solution builds with **zero new warnings** (per `CLAUDE.md` build-warning policy).
10. The `AgentSkillsProvider` produced by the hosted service contains exactly the expected skill set in a smoke test that runs every Monica module.

## 13. Cross-doc references

- Doc 01 (`01-knowledge-base-decoupling.md`) — declares `KnowledgeBaseLookupSkill : MoSkill<KnowledgeBaseLookupSkill>` and `RAGKnowledgeSkill : MoSkill<RAGKnowledgeSkill>` as worked examples consuming this doc's contracts.
- Doc 03 (`03-monica-facade-skill-provider.md`) — uses `MoSkill<TSelf>` via the explicit-override path to produce one module-level Skill per Monica module, with Facade-derived scripts grouped in the loaded skill content. The `[MoAITool]` attribute + description chain are unchanged.
- Doc 04 (`04-projectunit-skill-provider.md`) — same pattern as Doc 03 for ProjectUnit ApplicationServices. Reuses everything in this doc; adds CRUD-safety defaults on top.

---

**End of Doc 02.**
