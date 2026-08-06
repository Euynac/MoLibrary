# Doc 02 — Skill / Tool / MCP Base Classes + `[MoAITool]` Bridge

> **Status.** Design proposal. Spine of the Monica AI skill-system refactor.
> **Audience.** Framework team. Foundation that Docs 03 and 04 normatively reference.
> **Last revised.** 2026-08-06.

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

### 1.2 Monica's compiled type-discovery plan

Modules declare structural queries by overriding `DeclareTypeDiscovery(TypeDiscoveryPlan<TOptions>)`:

```csharp
public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleSkillSystemOption> discovery)
{
    discovery.Match(
        TypeQuery.ConcreteClass.AssignableTo<Skill>(),
        (context, matches) =>
        {
            foreach (var match in matches)
            {
                var skillType = match.Type;
                context.Registrations.TryAdd(
                    ServiceDescriptor.Singleton(skillType, skillType));
            }
        });
}
```

`ModuleRegistry` freezes every plan, compiles equivalent `TypeQuery` nodes, scans the host's business types once, and then invokes commit callbacks serially in module-topology and declaration order. Each result is a `BusinessTypeMatch`; its `BusinessTypeShape` lazily caches assignability, interfaces, base types, constructors, and attributes so consumers do not repeat reflection. High-volume commits mutate DI through `context.Registrations`, the indexed `ModuleServiceRegistrationWriter`.

This design uses that declarative plan. It does not add another global or chained per-module pass.

### 1.3 `IXmlDocumentationService`

`Monica.Core/XmlDocumentation/Abstractions/IXmlDocumentationService.cs`:

```csharp
public interface IXmlDocumentationService
{
    XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method);
    string? GetTypeDocumentation(Type type);
    void ClearCache();
    IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments();
}
```

`XmlMethodDocumentation` exposes the method `<summary>` plus a per-parameter dictionary keyed on parameter name. Module: `Monica.Core/Modules/ModuleXmlDocumentation.cs`. The skill-system's `Describe` override hard-requires `ModuleXmlDocumentation`, so this service is available when the capability catalog is built.

### 1.4 CLR module-type identity

Module composition and capability checks use the concrete module strategy `Type`. `ModuleKey` is derived diagnostic metadata only. A capability gate therefore stores module types directly:

```csharp
public override IReadOnlySet<Type> RequiredModules { get; } =
    new[] { typeof(ModuleRAG), typeof(ModuleVendorCustom) }.ToFrozenSet();
```

The loaded-module catalog exposes `IReadOnlySet<Type>`. There is no enum/string registry to synchronize and no fallback equality based on display names.

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
    /// Module strategy types this skill requires. The skill is silently skipped at registration
    /// when any required module is not loaded. Default: empty (no module gate).
    /// </summary>
    public virtual IReadOnlySet<Type> RequiredModules => FrozenSet<Type>.Empty;

    /// <summary>
    /// Hard-disable switch evaluated while the startup skill catalog is built. Default: true.
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

- `IReadOnlySet<Type> RequiredModules` — module gate.
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

    public override IReadOnlySet<Type> RequiredModules { get; } =
        new[] { typeof(ModuleRAG) }.ToFrozenSet();

    [MoAITool(
        Name = "search-knowledge-base",
        Description =
            "Semantic search over the selected knowledge bases. Returns ranked excerpts " +
            "with source name and source link for citation.")]
    public async Task<string> SearchAsync(
        [MoAITool(Description = "Focused retrieval query, not conversational text.")]
        string query,
        IServiceProvider services,
        [MoAITool(Description = "Optional original user question for context.")]
        string? userQuestion = null,
        [MoAITool(Description = "Top-K result count override.")]
        int? topK = null,
        CancellationToken ct = default)
    {
        var runtimeContext = services.GetRequiredService<IAIChatRuntimeContextAccessor>().Current;
        var selection = runtimeContext.GetOrDefault(KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection);
        if (selection is null || selection.KnowledgeBaseIds.Count == 0)
        {
            return JsonSerializer.Serialize(
                KnowledgeToolPayload.NoKnowledgeBaseSelected(),
                _toolJsonOptions);
        }

        var knowledgeBases = await ragService.LoadKnowledgeBasesAsync(
            selection.KnowledgeBaseIds,
            ct);

        var payload = await knowledgeToolService.SearchKnowledgeAsync(
            "search-knowledge-base", knowledgeBases, query, userQuestion, topK, ct);
        return JsonSerializer.Serialize(payload, _toolJsonOptions);
    }

    // ... three more scripts: browse-knowledge-documents, browse-knowledge-document-tree,
    //                         get-knowledge-document-content.
}
```

Notes on the migration:

- The four tool methods are now first-class C# methods on the skill class, marked with a single `[MoAITool(Name = "...", Description = "...")]`.
- If `Name` is omitted, Monica derives `search` from `SearchAsync` (kebab-case + drop `Async` suffix). Authors who want a longer name (e.g., `search-knowledge-base`) supply it explicitly.
- Per-session knowledge-base selection flows through `AIChatRuntimeContext` using the KnowledgeBase-owned `KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection` key. It does **not** live on `AIChatAgentCreateContext`, in skill frontmatter, or in loaded skill content.
- Script parameters stay user-facing-clean. Runtime-only parameters such as `IServiceProvider` and `CancellationToken` are hidden from the schema and used only to resolve scoped services and the current runtime context.
- Hard module gate via `RequiredModules`: the skill is silently skipped when `ModuleRAG` is not loaded. The Knowledge Base lookup-only Skill (Doc 01) requires `typeof(ModuleKnowledgeBase)`.

### 2.4 Lifecycle

- **Skill instance — singleton.** The discovery host registers each `MoSkill<TSelf>` subclass as a singleton in DI. `Frontmatter`, `Instructions`, `Resources`, and `Scripts` collections are evaluated once and cached.
- **Script invocation — scoped.** Microsoft's `[AgentSkillScript]` machinery resolves an `IServiceProvider` parameter (when present) by creating a per-invocation scope. Scoped dependencies (DbContext, UnitOfWork, ChatSessionContext) come through that scope.
- **No per-session skill rebuild.** The earlier rev 1 design proposed rebuilding the skill collection each session; rev 2 replaces that with Microsoft's progressive disclosure (frontmatter-only at L1, content on activation). This is simpler and matches the framework's intended use.
- **No per-session metadata.** Microsoft's `AgentSkillsProvider` caches generated `AIContext` by default (`DisableCaching = false`), including the skill advertisement prompt and generic `load_skill` / `read_skill_resource` / `run_skill_script` tools. Monica therefore treats skill metadata as static process-level data. Session-specific values are read only inside script/resource execution through `AIChatRuntimeContext`.

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
    /// Module strategy types this tool requires. The tool is silently skipped at
    /// registration when any required module is not loaded.
    /// </summary>
    public virtual IReadOnlySet<Type> RequiredModules => FrozenSet<Type>.Empty;

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

    public virtual IReadOnlySet<Type> RequiredModules => FrozenSet<Type>.Empty;

    public virtual bool IsEnabled => true;

    // MCP-specific surface — determined when the package is selected.
}
```

Discovery uses the same compiled `TypeDiscoveryPlan` as `MoSkill<TSelf>` and `MoTool`. The `[MoAITool]` description bridge applies uniformly to whatever method-shaped surfaces the chosen MCP package exposes.

### 4.3 Implementation note

Implementation of `MoMcp` is **deferred** until after Phase B of the implementation sequencing (see §11). The `MoSkill<TSelf>` and `MoTool` work is independent and ships first. The discovery plan described in §6 reserves a classification branch for `MoMcp` so the contract is forward-compatible.

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

- Derives from `MonicaModule<ModuleSkillSystemOption>`.
- Declares the discovery + filtering pipeline for `MoSkill<TSelf>`, `MoTool`, and `MoMcp` subclasses through `DeclareTypeDiscovery`.
- Builds the `AgentSkillsProvider` at startup via `AgentSkillsProviderBuilder.UseSkills(...)`.
- Declares hard dependencies on `ModuleAI` and `ModuleXmlDocumentation` in `Describe`, so graph shape is known before options or services are materialized.

`ModuleSkillSystem` implements neither `IWebModule` nor `IUIModule`; it contributes services and discovery only, so the same module can compose in generic and ASP.NET Core hosts.

Provisional declaration:

```csharp
public sealed class ModuleSkillSystem : MonicaModule<ModuleSkillSystemOption>
{
    private readonly List<Type> _skillTypes = [];
    private readonly List<Type> _toolTypes = [];
    private readonly List<Type> _mcpTypes = [];   // forward-compat slot

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleAI, ModuleAIOption>();
        module.Require<ModuleXmlDocumentation, ModuleXmlDocumentationOption>();
    }

    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleSkillSystemOption> discovery)
    {
        discovery.Match(
            TypeQuery.ConcreteClass,
            (_, matches) =>
            {
                foreach (var match in matches)
                {
                    var shape = match.Shape;
                    if (shape.BaseTypes.Any(IsClosedMoSkillBase))
                    {
                        _skillTypes.Add(match.Type);
                    }
                    else if (shape.IsAssignableTo(typeof(MoTool)))
                    {
                        _toolTypes.Add(match.Type);
                    }
                    else if (shape.IsAssignableTo(typeof(MoMcp)))
                    {
                        _mcpTypes.Add(match.Type);
                    }
                }
            });
    }

    public override void PostConfigureServices(ModuleContext<ModuleSkillSystemOption> context)
    {
        foreach (var skillType in _skillTypes)
        {
            context.Registrations.TryAdd(ServiceDescriptor.Singleton(skillType, skillType));
            context.Registrations.Add(ServiceDescriptor.Singleton(
                typeof(AgentSkill),
                sp => sp.GetRequiredService(skillType)));
        }

        foreach (var toolType in _toolTypes)
        {
            context.Registrations.TryAdd(ServiceDescriptor.Singleton(toolType, toolType));
            context.Registrations.Add(ServiceDescriptor.Singleton(
                typeof(AITool),
                sp => sp.GetRequiredService(toolType)));
        }

        // MCP registration deferred (see §4.3).

        context.Services.AddSingleton<IAgentSkillsProviderFactory, MonicaAgentSkillsProviderFactory>();
        context.Services.AddHostedService<MonicaSkillsProviderHostedService>();
    }

    private static bool IsClosedMoSkillBase(Type type)
    {
        return type.IsConstructedGenericType
               && type.GetGenericTypeDefinition() == typeof(MoSkill<>);
    }
}
```

The broad concrete-class query is compiled into Monica's single shared scan. The commit classifies its immutable matches through cached `BusinessTypeShape.BaseTypes` and `IsAssignableTo(...)` facts; it does not call `GetInterfaces()` or repeat assignability reflection per consumer. Registration happens in `PostConfigureServices`, after discovery commits are complete.

### 6.2 The `MonicaSkillsProviderHostedService`

A hosted service activated at startup:

1. Resolves all registered `AgentSkill` services.
2. Filters by `IsEnabled` and `RequiredModules` — for each `MoSkill<TSelf>` instance, drop it when any required module is not in the loaded module set (the loaded set is available through the module registry).
3. Calls `new AgentSkillsProviderBuilder().UseSkills(filtered).Build()` and registers the result as a singleton `AgentSkillsProvider` for the chat agent factory to consume.

Same flow for `AITool` (active tools list passed to the agent builder via `AIChatAgentBuilder.AddTool` — see §8).

### 6.3 Registration entrypoint

```csharp
public static class ModuleSkillSystemBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Enables Monica's class-based AI skill / tool / MCP discovery and registration.
        /// Discovers all <see cref="MoSkill{TSelf}"/>, <see cref="MoTool"/>, and
        /// <see cref="MoMcp"/> subclasses through Monica's compiled type-discovery plan,
        /// builds a Microsoft.Agents.AI.AgentSkillsProvider, and exposes the
        /// resulting capability set to the chat agent.
        /// </summary>
        public ModuleRegistration<ModuleSkillSystem, ModuleSkillSystemOption> AddAISkillSystem(
            Action<ModuleSkillSystemOption>? action = null)
        {
            return builder.AddModule<ModuleSkillSystem, ModuleSkillSystemOption>(action);
        }
    }
}
```

The Facade Provider (Doc 03) and ProjectUnit Provider (Doc 04) declare `module.Require<ModuleSkillSystem, ModuleSkillSystemOption>()` from their `Describe` overrides.

## 7. Lifecycle and DI reconciliation

| Concept | Lifetime | Notes |
|---|---|---|
| `MoSkill<TSelf>` instance | Singleton | Frontmatter, Instructions, RequiredModules, IsEnabled evaluated once. |
| `[MoAITool]` script invocation | Per-call (scoped via `IServiceProvider` parameter) | Same `CreateScript`-backed pipeline Microsoft uses for `[AgentSkillScript]`; Monica's discovery just chooses the marker. |
| `MoTool` instance | Singleton | Same as Skill. |
| `MoTool.InvokeAsync` invocation | Per-call (scoped via `IServiceProvider` parameter) | Wired through `AIFunctionFactory.Create`. |
| `MoMcp` | TBD when package is finalized | Reserve singleton + per-call scope as the default. |
| `AgentSkillsProvider` | Singleton, built once at startup | Skill set is immutable per process. Hot-reload is out of scope. |

Module gate evaluation is **startup-only**, after the compiled module graph and skill catalog are available. Skills whose `RequiredModules` aren't satisfied are dropped before any `AgentSkillsProvider` is built. A skill cannot become enabled mid-process by loading a new module dynamically — Monica modules are composed at startup and that is when the gate fires.

### 7.1 Runtime context contract

`AIChatAgentCreateContext` is construction-only. It carries the inputs needed to build the agent pipeline, such as base instructions, but it must not grow module-specific session properties. `KnowledgeBaseIds` is explicitly rejected here because it makes the base AI module depend on RAG and conflicts with the cached `AgentSkillsProvider` lifecycle.

Per-session and per-run state flows through a new `AIChatRuntimeContext`:

```csharp
public sealed class AIChatRuntimeContext
{
    public static AIChatRuntimeContext Empty { get; }

    public AIChatRuntimeContext Set<T>(AIChatRuntimeContextKey<T> key, T value);
    public bool TryGet<T>(AIChatRuntimeContextKey<T> key, out T value);
    public T? GetOrDefault<T>(AIChatRuntimeContextKey<T> key);
}

public sealed record AIChatRuntimeContextKey<T>(string Name);

public interface IAIChatRuntimeContextAccessor
{
    AIChatRuntimeContext Current { get; }
}
```

`AIChatRuntimeContext` is immutable: `Set` returns a new context snapshot. `ChatSession` owns the current snapshot. `AIChatService` pushes that snapshot into an ambient accessor for the duration of the async `RunAsync` / `RunStreamingAsync` call, then restores the previous value in `finally`. The accessor is registered as a singleton facade over `AsyncLocal<AIChatRuntimeContext>` so nested script invocation scopes still see the same per-run context. Script and resource methods that need session state resolve `IAIChatRuntimeContextAccessor` from their `IServiceProvider` parameter and read typed keys from `Current`.

Module-owned feature state is modeled by module-owned keys. KnowledgeBase defines the shared selection used by lookup and RAG scripts:

```csharp
public sealed record KnowledgeBaseSelection(IReadOnlyList<string> KnowledgeBaseIds);

public static class KnowledgeBaseChatRuntimeContextKeys
{
    public static readonly AIChatRuntimeContextKey<KnowledgeBaseSelection> KnowledgeSelection =
        new("knowledge-base.selection");
}
```

The AI UI maps its selected knowledge-base IDs into `KnowledgeBaseSelection` and stores it on the session runtime context. `KnowledgeBaseLookupSkill` can use the same selection for lookup-only browsing, while `RAGKnowledgeSkill` filters the selected KBs to those with an embedding binding before semantic search. If no RAG-enabled selection is present, the RAG script returns a clear no-selection payload instead of relying on skill availability, provider rebuilds, or prompt changes.

`AgentSkillsProviderOptions.DisableCaching = true` is not the fix for session state. It may be useful for development diagnostics or truly dynamic skill catalogs, but production session variance must be represented by `AIChatRuntimeContext` so the skill advertisement prompt and generic skill tools remain cacheable and non-leaky.

## 8. Migrating the existing `IAIChatToolProvider` surface

### 8.1 Migration target: `KnowledgeSearchToolProvider`

The existing provider becomes `Monica.AI.RAG.Skills.RAGKnowledgeSkill : MoSkill<RAGKnowledgeSkill>` (skeleton in §2.3). Migration steps, all in a single PR:

1. Move the four nested local methods (`SearchKnowledgeAsync`, `BrowseKnowledgeDocumentsAsync`, `BrowseKnowledgeDocumentTreeAsync`, `GetKnowledgeDocumentContentAsync`) up to instance methods on the new skill class.
2. Annotate each with `[MoAITool(Name = "kebab-name", Description = "...")]`. No `[AgentSkillScript]`.
3. Remove the manual `KnowledgeBase[]` plumbing and `AIChatAgentCreateContext.KnowledgeBaseIds`. Resolve `IAIChatRuntimeContextAccessor` from the `IServiceProvider` script parameter, read `KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection`, and load the selected knowledge bases inside the script invocation.
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
| Per-session tool collection wired through `AIChatAgentBuilder.AddTool` and `AIChatAgentCreateContext.KnowledgeBaseIds` | `AgentSkillsProvider` built once at startup; selected KBs flow through `AIChatRuntimeContext` at script execution time |

## 11. Open questions and resolutions

| # | Question | Resolution |
|---|---|---|
| (a) | Evaluate `IsEnabled` / `RequiredModules` per session, or only while building the startup catalog? | **Startup only.** Cheap, predictable, no surprise gating mid-conversation. |
| (b) | Add a Monica-specific class-level marker attribute (`[MoSkill]` parameterless)? | **No.** CRTP via `: MoSkill<TSelf>` already gives the marker. |
| (c) | Where do the three base classes physically live? | **`Monica.AI/Skills/Abstractions/`** (new `Skills` feature folder under `Monica.AI`). The annotations live in `Monica.AI/Skills/Annotations/`. The consolidated module registration lives at `Monica.AI/Modules/ModuleSkillSystem.cs`. |
| (d) | Multi-level inheritance (e.g., `SpecialSkill : RAGKnowledgeSkill`) — does discovery still work? | **No.** Microsoft's CRTP discovery reflects only on `TSelf`. Each leaf must re-apply CRTP: `class SpecialSkill : MoSkill<SpecialSkill>`. The doc cites Microsoft's documented limitation. |
| (e) | MCP package selection. | **Verify-before-implement.** Doc 02 reserves the slot; the implementation phase pins the package after inspecting Microsoft's MCP integration release at that time. |
| (f) | Forward-compat `[MoAITool(RequiredPermissions = ...)]` slot. | **Not in this rev.** A future security doc owns it. The attribute does not ship the property; adding it later is a non-breaking change because attribute properties are additive. |
| (g) | Should selected knowledge bases live on `AIChatAgentCreateContext`? | **No.** `AIChatAgentCreateContext` is construction-only and module-neutral. Selected KBs are KnowledgeBase-owned runtime state under `AIChatRuntimeContext`, with RAG filtering to embedding-bound KBs at script execution time. |

## 12. Acceptance criteria for implementation

When Phase B (this doc) is implemented, the following must hold:

1. `Monica.AI/Skills/Abstractions/MoSkill.cs`, `MoTool.cs` exist. `MoMcp.cs` is a placeholder file with a `// TODO: package selection` comment.
2. `Monica.AI/Skills/Annotations/MoAIToolAttribute.cs` exists with `Name`, `Description`, and `Disabled` properties; `AttributeUsage` constrained to method + parameter.
3. `Monica.AI/Skills/Internal/MoSkillScriptDiscovery.cs` exists and implements the `[MoAITool]`-marker-based reflection used by `MoSkill<TSelf>.Scripts`. The auto-derived kebab-case name rule is unit-tested.
4. `Monica.AI/Modules/ModuleSkillSystem.cs` derives from `MonicaModule<ModuleSkillSystemOption>`, declares its `TypeDiscoveryPlan` in `DeclareTypeDiscovery`, and registers the services that build `AgentSkillsProvider`.
5. `Monica.AI.RAG.Skills.RAGKnowledgeSkill` migrated from `KnowledgeSearchToolProvider`. `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs` deleted. `services.TryAddEnumerable(...)` registration removed.
6. `Monica.AI/Abstractions/IAIChatToolProvider.cs` **deleted**. No shim, no `[Obsolete]` adapter. `Monica.AI/Services/Support/AIChatAgentBuilder.cs` no longer exposes `AddTool(AITool)`.
7. The `IXmlDocumentationService.GetMethodDocumentation` path is exercised by at least one method per skill in tests (compile-time verification: every Facade-method-style script has a description in one of the three sources).
8. `ModuleRAG` no longer registers `KnowledgeSearchToolProvider`; the new `RAGKnowledgeSkill` is auto-discovered.
9. `AIChatAgentCreateContext` has no `KnowledgeBaseIds` or other module-specific feature state; `AIChatRuntimeContext`, `AIChatRuntimeContextKey<T>`, and `IAIChatRuntimeContextAccessor` carry typed per-session/per-run state.
10. KnowledgeBase declares `KnowledgeBaseSelection` and `KnowledgeBaseChatRuntimeContextKeys.KnowledgeSelection`; lookup and RAG skills read that key at script execution time, with RAG returning a clear no-selection payload when no selected KB is RAG-enabled.
11. Solution builds with **zero new warnings** (per `CLAUDE.md` build-warning policy).
12. The `AgentSkillsProvider` produced by the hosted service contains exactly the expected skill set in a smoke test that runs every Monica module, and the generated skill advertisement/content does not vary by selected KB.

## 13. Cross-doc references

- Doc 01 (`01-knowledge-base-decoupling.md`) — declares `KnowledgeBaseLookupSkill : MoSkill<KnowledgeBaseLookupSkill>` and `RAGKnowledgeSkill : MoSkill<RAGKnowledgeSkill>` as worked examples consuming this doc's contracts.
- Doc 03 (`03-monica-facade-skill-provider.md`) — uses `MoSkill<TSelf>` via the explicit-override path to produce one module-level Skill per Monica module, with Facade-derived scripts grouped in the loaded skill content. The `[MoAITool]` attribute + description chain are unchanged.
- Doc 04 (`04-projectunit-skill-provider.md`) — same pattern as Doc 03 for ProjectUnit ApplicationServices. Reuses everything in this doc; adds CRUD-safety defaults on top.

---

**End of Doc 02.**
