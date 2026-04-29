# Doc 04 — ProjectUnit AI Skill Provider (business-facing)

> **Status.** Design proposal. Phase D of the implementation roadmap.
> **Audience.** Business-domain teams writing `ApplicationService` / `CrudApplicationService` subclasses — readable without framework expertise.
> **Depends on.** Doc 02 (`MoSkill<TSelf>`, `[MoAITool]`, description-priority chain), Doc 03 (Facade auto-projection mechanism, `Res<T>` unwrap rule).
> **Last revised.** 2026-04-29.

## 0. Why this doc exists

Monica's business layer expresses use cases through `ApplicationService` subclasses (the Command / Query pattern, with a single `Handle(TRequest, CancellationToken)` method) and through `CrudApplicationService<TEntity, ...>` subclasses (which expose the full Create / Read / Update / Delete surface for an entity). These are the natural agent-callable units in any Monica business app: "place an order," "list pending invoices," "get customer by id."

Today, none of them are visible to the chat agent. A domain team that wants to expose `OrderApplicationService.Handle(PlaceOrderRequestDto, ...)` as an agent capability has the same boilerplate problem the Facade Provider (Doc 03) solves for framework Facades: hand-roll an `IAIChatToolProvider`, marshal the method through `AIFunctionFactory.Create`, register manually.

This doc specifies a generalized mechanism: a new module `ModuleAIProjectUnitProvider` in `Monica.Framework` that auto-projects every business `ApplicationService` and `CrudApplicationService` discovered by the existing `ModuleProjectUnits` `IBusinessTypeIterator` into one *Skill per ProjectUnit*. The script set inside that Skill mixes `Handle` methods (one per Command/Query application service in the unit) with CRUD methods (one per `CrudApplicationService` exposing read / opt-in mutating ops).

The most important business-safety call in the entire refactor lives in this doc: **mutating CRUD methods (Create / Update / Delete / BulkDelete) are opt-in.** An agent must not delete a row by default.

## 1. Premise

### 1.1 Reference types

| Type | File | Role |
|---|---|---|
| `ProjectUnit` | `Monica.Framework/ProjectUnits/Models/ProjectUnit.cs` | Abstract base for every business unit; carries `Type`, `Title`, `Description`, `Methods`, `Group`. |
| `ModuleProjectUnits` | `Monica.Framework/ProjectUnits/Modules/ModuleProjectUnits.cs` | Implements `IBusinessTypeIterator`; already iterates business types and builds a `ProjectUnitRegistry`. |
| `ApplicationService` | `Monica.WebApi/Abstractions/ApplicationService.cs` | Base for business application services (Command / Query). |
| `CustomApplicationService<TRequest, TResponse>` | same file | Parent of all single-Handle services. |
| `ApplicationService<TRequest, TResponse>` | same file | Returns `Res<TResponse>`; the typical Query / Command shape. |
| `ApplicationService<TRequest>` | same file | Returns `Res`; the typical mutating Command shape. |
| `CrudApplicationService<TEntity, ...>` | `Monica.WebApi/AutoControllers/Services/CrudApplicationService.cs` | Multi-method CRUD service; exposes `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `BulkDeleteAsync`, `GetAsync`, `ListAsync`. |

### 1.2 The discovery hook is already there

`ModuleProjectUnits.IterateBusinessTypes` already walks every business assembly and produces a typed `ProjectUnitRegistry`. The Provider in this doc piggybacks on that registry — it does not re-iterate. New work is purely the projection of each registered `ProjectUnit` into an `AgentSkill`.

This makes Phase D the cheapest of the four phases: no new iteration pass, no new marker interface, no parallel registry.

### 1.3 Module placement

`Monica.Framework/AISkillProviders/Facade/` already exists from Doc 03. Doc 04 adds a sibling folder:

```
Monica.Framework/
  AISkillProviders/
    ProjectUnit/
      Modules/
        ModuleAIProjectUnitProvider.cs
        ModuleAIProjectUnitProviderGuide.cs
        ModuleAIProjectUnitProviderOption.cs
      Internal/
        ProjectUnitSkill.cs              // AgentClassSkill subclass per ProjectUnit
        ProjectUnitMethodScanner.cs      // walks ApplicationService + CrudApplicationService surfaces
        ProjectUnitScriptFactory.cs      // builds AgentSkillScript via CreateScript
```

Module key: add `BuiltInModuleKey.AIProjectUnitProvider` (next to `AIFacadeProvider` from Doc 03) in `BuiltInModuleKey.cs`.

### 1.4 Dependency claims

```csharp
public override void ClaimDependencies()
{
    DependsOnModule<ModuleAIGuide>().Register();
    DependsOnModule<ModuleSkillSystemGuide>().Register();
    DependsOnModule<ModuleProjectUnitsGuide>().Register();    // for the registry
    DependsOnModule<ModuleXmlDocumentationGuide>().Register();
}
```

## 2. Skill granularity — one Skill per ProjectUnit

A *ProjectUnit Skill* represents a bounded context. Its scripts are the union of:

- For each `Custom*ApplicationService<TRequest, ...>` in the unit → **one script per Handle method**.
- For each `CrudApplicationService<TEntity, ...>` in the unit → **multiple scripts**, one per CRUD method that survives the read/mutating filter (§4).

This matches the user's "Skill is the parent concept" framing and how a domain expert thinks about a bounded context. An `OrderProjectUnit` becomes a single `order` skill in the system prompt; the agent picks it when the user wants to do anything with orders. The L2 expansion lists the unit's available scripts (place-order, list-orders, get-order-by-id, …).

This is **not** one Skill per ApplicationService. Spreading 15 small Skills across the system prompt for a single bounded context defeats progressive disclosure.

## 3. Mechanism — projection of a single ProjectUnit

For each `ProjectUnit pu` in the registry:

1. Determine the unit's `ApplicationService`-derived members (§3.1).
2. For each member, run the script-generation pipeline (§3.2 for single-Handle services, §3.3 for `CrudApplicationService`).
3. Collect resulting `AgentSkillScript`s.
4. Build a `ProjectUnitSkill` (the `AgentClassSkill<ProjectUnitSkill>` subclass mirroring Doc 03's `FacadeSkill`).
5. Frontmatter and instructions per §3.4.

### 3.1 Identifying the ApplicationServices in a ProjectUnit

`ProjectUnit.DependencyUnits` contains other units the current unit depends on. The ApplicationServices belonging *to* this unit are the unit's own `Type` (when the type is itself an `ApplicationService` subclass) and any `IApplicationService`-marked types in the unit's owning namespace. The exact rule:

> An `ApplicationService`-derived type belongs to ProjectUnit `pu` when:
> - The type's full name starts with `pu.Type.Namespace`, AND
> - The type implements `IApplicationService`.

This ties units to a contiguous namespace, which is how Monica's existing ProjectUnit registry already groups things. Edge cases (services that span two unit namespaces) get a warning at registration and the service is assigned to the longest-prefix-matching unit.

### 3.2 Single-Handle ApplicationService projection

For `class PlaceOrderApplicationService : ApplicationService<PlaceOrderRequestDto, OrderId>`, the projection produces **one script**:

| Field | Source |
|---|---|
| Script name | kebab-case of `TRequest` type name with the `RequestDto` / `Command` / `Query` suffix stripped. Example: `PlaceOrderRequestDto` → `place-order`; `ListInvoicesQuery` → `list-invoices`. |
| Description | Doc 02 §5.2 priority chain on the `Handle` method first, falling back to the same chain on the `TRequest` type. (Often the `Handle` itself has only `<inheritdoc/>` and the meaningful XML doc is on the request DTO.) |
| Parameters | Properties of `TRequest`. Each property's type, default, required-ness comes from C# `init` / `required` semantics. Each property's description is resolved through the priority chain on the property declaration. |

Implementation: the script delegate accepts a synthesized `TRequest` whose properties are spread as JSON-schema entries. `AIFunctionFactory.Create` already handles DTO parameter unpacking when given a delegate; the Provider just constructs the right delegate signature.

#### 3.2.1 Worked example

```csharp
namespace Sample.Orders;

public sealed class PlaceOrderRequestDto : IResultRequest<OrderId>
{
    /// <summary>Customer placing the order.</summary>
    [Required]
    public required string CustomerId { get; init; }

    /// <summary>Items to order. Must contain at least one item.</summary>
    [MinLength(1)]
    public required IReadOnlyList<OrderItemDto> Items { get; init; }

    /// <summary>Optional discount code to apply at checkout.</summary>
    public string? DiscountCode { get; init; }
}

public sealed class PlaceOrderApplicationService(IOrderRepository repo, IClock clock)
    : ApplicationService<PlaceOrderRequestDto, OrderId>
{
    public override async Task<Res<OrderId>> Handle(
        PlaceOrderRequestDto request, CancellationToken ct)
    {
        // ...
    }
}
```

After projection (assuming the Order ProjectUnit holds this service):

```json
{
  "name": "place-order",
  "description": "<Handle XML summary, falling back to PlaceOrderRequestDto's>",
  "parameters": {
    "type": "object",
    "properties": {
      "customerId":   { "type": "string",  "description": "Customer placing the order." },
      "items":        { "type": "array",   "description": "Items to order. Must contain at least one item.", "minItems": 1, "items": { "$ref": "#/definitions/OrderItemDto" } },
      "discountCode": { "type": ["string","null"], "description": "Optional discount code to apply at checkout." }
    },
    "required": ["customerId", "items"]
  }
}
```

The `Required` / `MinLength` `DataAnnotations` propagate into the JSON schema (§7.1).

### 3.3 `CrudApplicationService` projection

For `class CustomerCrudAppService : CrudApplicationService<Customer, CustomerDto, Guid, CustomerListInput, CustomerCreateInput, CustomerUpdateInput, CustomerRepository>`, the projection produces multiple scripts depending on the read/mutating filter:

| Method | Default | If `[MoAITool]` is present (any properties) |
|---|---|---|
| `GetAsync(TKey id)` | exposed | exposed (with optional description override) |
| `ListAsync(TGetListInput input)` | exposed | exposed |
| `CreateAsync(TCreateInput input)` | **NOT exposed** | exposed |
| `UpdateAsync(TKey id, TUpdateInput input)` | **NOT exposed** | exposed |
| `DeleteAsync(TKey id)` | **NOT exposed** | exposed |
| `BulkDeleteAsync(TBulkDeleteInput input)` | **NOT exposed** | exposed |

`[MoAITool(Disabled = true)]` overrides in either direction — a Get / List can be force-disabled, a Create can be force-enabled by adding `[MoAITool]` (with or without other properties).

The default-deny on mutating operations is the most important business-safety choice in the whole refactor. The doc-writer of Phase D is responsible for surfacing this prominently in any release notes or onboarding doc.

#### 3.3.1 Why opt-in for mutating ops?

Three reasons:

1. **Latency of consequence.** A bad agent call to `Get` returns wrong data; a bad call to `Delete` removes a row. The blast radius is asymmetric.
2. **Reasoning cost.** Most agents are not yet reliable enough for unsupervised CRUD over business data. Forcing explicit opt-in pushes the author to think "do I really want the agent to do this?"
3. **Auditability.** Grep for `[MoAITool]` on a `*CrudApplicationService` to find every mutating method exposed to agents in the codebase.

#### 3.3.2 Worked example

```csharp
public sealed class CustomerCrudAppService(CustomerRepository repo)
    : CrudApplicationService<Customer, CustomerDto, Guid,
                             CustomerListInput, CustomerCreateInput, CustomerUpdateInput,
                             CustomerRepository>(repo)
{
    // GetAsync, ListAsync — auto-exposed.

    [MoAITool(Description = "Create a customer record. Use only when the user explicitly requests creation.")]
    public override Task<Res> CreateAsync(CustomerCreateInput input)
        => base.CreateAsync(input);

    // UpdateAsync, DeleteAsync, BulkDeleteAsync — NOT exposed (no [MoAITool]).
}
```

After projection: scripts `get`, `list`, `create`. `update`, `delete`, `bulk-delete` are absent.

### 3.4 Frontmatter and Instructions on the ProjectUnit Skill

```csharp
namespace Monica.Framework.AISkillProviders.ProjectUnit.Internal;

internal sealed class ProjectUnitSkill(
    ProjectUnit projectUnit,
    AgentSkillFrontmatter frontmatter,
    string instructions,
    IReadOnlyList<AgentSkillScript> scripts)
    : MoSkill<ProjectUnitSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = frontmatter;
    protected override string Instructions { get; } = instructions;

    /// <summary>
    /// Pre-built script list supplied by the Provider's reflection pass.
    /// The override short-circuits MoSkill's [MoAITool] discovery — the Provider's
    /// scanner already accounts for read/mutating CRUD rules and Handle methods,
    /// so attribute-driven discovery would double up.
    /// </summary>
    public override IReadOnlyList<AgentSkillScript>? Scripts { get; } = scripts;

    public ProjectUnit ProjectUnit { get; } = projectUnit;
}
```

Inherits `MoSkill<ProjectUnitSkill>` (same base as the Facade Provider's `FacadeSkill` from Doc 03 §2.1) for consistency with hand-written Skills.

Frontmatter:
- **Name.** `unit-{kebab(projectUnit.Title)}`. Example: `unit-orders`, `unit-customer`.
- **Description.** `projectUnit.Description` if non-empty (Title's description is already filled by `ProjectUnitDocumentationResolver.ExtractTypeDescription`); otherwise fallback to `"The {projectUnit.Title} bounded context."`.

Instructions template:

```
This bounded context exposes the following operations:

- {script-name-1}: {description-1}
- {script-name-2}: {description-2}
- ...

{mutation-warning-line}
```

Where `{mutation-warning-line}` is included only when at least one mutating CRUD script is in the surviving set. Text:

```
Some operations modify business data (create, update, delete). Confirm intent
with the user before invoking them and surface the result clearly.
```

This is a soft prompt addition. The hard safety mechanism is the opt-in itself; this is the L2 reminder.

## 4. Default exposure rule — the load-bearing safety call

Repeated for emphasis (the doc-writer of Phase D reproduces this verbatim in implementation guidance):

> **Read methods on `CrudApplicationService` (Get, List, BulkGet variants) are auto-exposed.**
> **Mutating methods on `CrudApplicationService` (Create, Update, Delete, BulkDelete) require an explicit `[MoAITool]` annotation on the override to be exposed.**
> **Custom `ApplicationService` subclasses' single Handle method is auto-exposed by default — the framework cannot infer "mutating" from a generic Handle. Authors are responsible for adding `[MoAITool(Disabled = true)]` on Handle methods whose action should not be agent-callable.**

The asymmetric default exists *only* for `CrudApplicationService` because there the framework knows mutation by method name. For `ApplicationService<TRequest, TResponse>` and `ApplicationService<TRequest>` the framework defaults to expose because there is no reliable way to classify Handle as read or write.

### 4.1 Author guidance — opting out of risky Handle methods

If a `Handle` performs a destructive action, the author *must* either:

- Add `[MoAITool(Disabled = true)]` on the Handle method.
- Or rename the request DTO / convert the service to inherit a project-defined `MutatingApplicationService<TRequest>` marker the project chooses to filter out (see §10 open question (a)).

The doc-writer of Phase D documents this in the new `Monica.Framework` release notes as a checklist for projects that use `Mo.AddAIProjectUnitSkills().UseAllUnits()` — every Handle that mutates state must be reviewed.

## 5. Activation

A ProjectUnit Skill activates whenever `ModuleAIProjectUnitProvider` is loaded and the unit was discovered. There is **no per-session permission gate** in this rev. The forward-compatibility slot for permission gating is reserved on `MoSkill<TSelf>` (Doc 02 §5.1 — the `RequiredPermissions` property on `[MoAITool]` is documented but not shipped) and a future security doc owns the design.

For now, projects that need permission gating implement it inside individual `Handle` methods or `CrudApplicationService` overrides — same as today's API authorization pattern. The ProjectUnit Skill does not enforce permission at the script level.

## 6. Discovery integration

The Provider's iterator step:

```csharp
public sealed class ModuleAIProjectUnitProvider(ModuleAIProjectUnitProviderOption option, IProjectUnitRegistry units)
    : ModuleBase<ModuleAIProjectUnitProvider, ModuleAIProjectUnitProviderOption, ModuleAIProjectUnitProviderGuide>(option),
      IBusinessTypeIterator
{
    private readonly List<ProjectUnit> _selectedUnits = [];

    // No new type filtering is needed; ModuleProjectUnits already populated the registry.
    // This iterator pass is a no-op for type discovery and just yields all types through.
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types) yield return type;
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        // Read the ProjectUnits the Guide selected.
        var allUnits = units.GetAll();
        _selectedUnits.AddRange(option.RegistrationMode switch
        {
            ProjectUnitRegistrationMode.All       => allUnits,
            ProjectUnitRegistrationMode.Selective => allUnits.Where(u => option.AllowedUnitTypes!.Contains(u.Type)),
            _ /* None */                          => Enumerable.Empty<ProjectUnit>()
        });

        foreach (var unit in _selectedUnits)
        {
            var skill = ProjectUnitScriptFactory.BuildSkill(unit, option, services.BuildServiceProvider());
            services.AddSingleton<AgentSkill>(_ => skill);
        }
    }
}
```

Key design point: the Provider does **not** re-implement type discovery. It consults the existing `ProjectUnitRegistry` (populated by `ModuleProjectUnits` during its own iteration phase). This honors Monica's "one source of truth per concern" rule and avoids duplicate scanning.

## 7. RequestDto validation propagation

### 7.1 What flows into the JSON schema

Microsoft's `AIFunctionFactory.Create` already inspects the parameter types of the supplied delegate and emits a JSON schema based on:

- The C# type (string, int, nullable variants, arrays, dictionaries, custom records).
- `[Description]` on the parameter or property.
- `[Required]` (DataAnnotations).
- C# `required` modifier on the property.
- Nullable annotations.
- Default parameter values.

Doc 04 declares these `DataAnnotations` as **mapped through to the schema** when the script delegate's parameter is a `RequestDto`-style class:

| C# annotation | JSON schema effect |
|---|---|
| `[Required]` | Property is in `"required"` list. |
| `required` modifier | Property is in `"required"` list. |
| `[StringLength(min, max)]` | `"minLength"`, `"maxLength"` on string property. |
| `[MinLength(n)]` | `"minLength"` on string; `"minItems"` on collection. |
| `[MaxLength(n)]` | `"maxLength"` on string; `"maxItems"` on collection. |
| `[Range(min, max)]` | `"minimum"`, `"maximum"` on number. |
| `[RegularExpression(pattern)]` | `"pattern"` on string. |
| `[EmailAddress]` | `"format": "email"` on string. |
| `[Url]` | `"format": "uri"` on string. |
| `[DataType(DataType.Date)]` | `"format": "date"` on string. |

Other `DataAnnotations` are not mapped in this rev. Projects that need richer mappings (e.g., `[Compare]`) document the gap in their own onboarding docs.

### 7.2 Recursion and cycle handling

Recursion depth defaults to 3 (same as the Facade Provider's `MaxParameterSchemaDepth`). DTOs that reference each other cyclically are detected by the schema builder and pruned at the cycle boundary — the property's type degrades to `{ "type": "object" }` with no nested schema, and a warning is logged at registration time.

### 7.3 What does NOT propagate

- Custom validators implementing `IValidatableObject`. The agent has no visibility into runtime validation; failures bubble up as `MoAIToolFailureException` at script invocation time (per Doc 03 §8).
- FluentValidation rules (if a project uses FluentValidation alongside `DataAnnotations`). Same reason.

The doc-writer of Phase D documents this gap explicitly so projects don't expect FluentValidation rules to be visible to the agent.

## 8. Selective registration — guide methods

Mirror Doc 03's pattern:

```csharp
public sealed class ModuleAIProjectUnitProviderGuide
    : ModuleGuide<ModuleAIProjectUnitProvider, ModuleAIProjectUnitProviderOption, ModuleAIProjectUnitProviderGuide>
{
    private const string CONFIG_REGISTRATION = nameof(CONFIG_REGISTRATION);
    protected override string[] GetRequestedConfigMethodKeys() => [CONFIG_REGISTRATION];

    /// <summary>Register every discovered ProjectUnit as a ProjectUnit Skill.</summary>
    public ModuleAIProjectUnitProviderGuide UseAllUnits()
    {
        ConfigureModuleOption(opt =>
        {
            opt.RegistrationMode = ProjectUnitRegistrationMode.All;
            opt.AllowedUnitTypes = null;
        });
        ConfigureEmpty(CONFIG_REGISTRATION);
        return this;
    }

    /// <summary>Register only the listed ProjectUnit types.</summary>
    public ModuleAIProjectUnitProviderGuide UseUnits(params Type[] projectUnitTypes)
    {
        ArgumentNullException.ThrowIfNull(projectUnitTypes);
        ConfigureModuleOption(opt =>
        {
            opt.RegistrationMode = ProjectUnitRegistrationMode.Selective;
            opt.AllowedUnitTypes = projectUnitTypes.ToHashSet();
        });
        ConfigureEmpty(CONFIG_REGISTRATION);
        return this;
    }
}
```

`Mo` extension:

```csharp
public static class ModuleAIProjectUnitProviderBuilderExtensions
{
    extension(Mo)
    {
        public static ModuleAIProjectUnitProviderGuide AddAIProjectUnitSkills(
            Action<ModuleAIProjectUnitProviderOption>? action = null)
        {
            return new ModuleAIProjectUnitProviderGuide().Register(action);
        }
    }
}
```

Per-method exclusion (`[MoAITool(Disabled = true)]`) is in code, same as Doc 03.

## 9. `Res<T>` unwrap semantics

Identical to Doc 03 §8. Reproduced here for clarity:

| Method return type | Script return | Failure handling |
|---|---|---|
| `Task<Res>` | `"OK"` (or `Res.Message`) | `IsFailed` → throw `MoAIToolFailureException` |
| `Task<Res<T>>` | JSON `T` | `IsFailed` → throw `MoAIToolFailureException` |
| `Task<ResPaged<T>>` | JSON `{ items, total, ...paging }` (paging metadata flattened from `ResPaged`) | Same |
| `Task<TResponse>` (no `Res` wrapper) | JSON `TResponse` | Native exception bubbles as tool error |

`ResPaged<dynamic>` (used by `CrudApplicationService.ListAsync`) is given special treatment: the `Items` collection is unwrapped along with paging metadata so the agent sees a coherent paged result rather than a wrapper envelope.

## 10. Open questions and resolutions

| # | Question | Resolution |
|---|---|---|
| (a) | Should Command vs Query services produce visually distinct tool groupings? | **No, naming convention only.** This rev does not add a Command/Query sub-Skill nesting. Authors who want grouping use prefix conventions in script names (e.g., `query-list-orders` vs `command-place-order`); that's a project-level choice. Adding sub-Skill nesting is a future iteration. |
| (b) | Should the doc specify a soft cap on per-Skill script count? | **Soft cap of 30, log-warn beyond.** Set in `ModuleAIProjectUnitProviderOption.MaxScriptsPerSkillSoftCap = 30`. When a unit produces more than 30 scripts, the Provider logs `"ProjectUnit '{name}' produced {n} scripts; consider splitting the bounded context"`. The cap is *not* a hard limit — agents can still consume the skill — but the warning surfaces design pressure. |
| (c) | Async vs sync activation predicate. | **Sync iterator-phase only**, same as Doc 02 §11(a). |
| (d) | What about ProjectUnits whose ApplicationServices live in multiple namespaces? | **Longest-prefix wins** (§3.1). Boundary cases warn at registration. The doc-writer of Phase D documents this rule in onboarding so domain teams structure their namespaces deliberately. |
| (e) | What about `BulkDeleteAsync` when `TBulkDeleteInput == CrudDisableDto`? | **Auto-skipped.** The `CrudDisableDto` sentinel signals "this CRUD service has bulk delete disabled" — `BulkDeleteAsync` is not in the projection set even with `[MoAITool]`. |
| (f) | What about `CustomApplicationService` that returns a non-`Res` envelope (e.g., a raw `int`)? | **Auto-exposed and serialized as JSON.** §9 handles this row. The unwrap rule kicks in only when `Res` / `Res<T>` / `ResPaged<T>` is the declared return type. |
| (g) | Method-name collisions inside a single ProjectUnit Skill. | **Forbidden.** Two ApplicationServices that produce the same script name (e.g., both `place-order`) cause the Provider to throw at startup with a clear message naming both source types. Authors fix the collision by renaming one of the request DTOs. |
| (h) | Permission gates per script. | **Out of scope this rev.** The forward-compat `[MoAITool(RequiredPermissions = ...)]` slot reserved by Doc 02 §5.1 covers this. A future security doc owns the implementation. |

## 11. Acceptance criteria for Phase D

When Phase D is implemented:

1. `Monica.Framework/AISkillProviders/ProjectUnit/` folder exists with all files listed in §1.3.
2. `BuiltInModuleKey.AIProjectUnitProvider` exists.
3. `Mo.AddAIProjectUnitSkills().UseAllUnits()` and `Mo.AddAIProjectUnitSkills().UseUnits(...)` are callable from a host `Program.cs`.
4. End-to-end smoke test: a sample business app with one Custom ApplicationService (PlaceOrderApplicationService) and one CrudApplicationService (CustomerCrudAppService) registers `Mo.AddAIProjectUnitSkills().UseAllUnits()`. The agent's system prompt contains `unit-orders` and `unit-customer` skills (or whatever the unit titles are). The order skill has `place-order` script. The customer skill has `get` and `list` scripts but **NOT** `create`, `update`, `delete`, `bulk-delete`.
5. Adding `[MoAITool(Description = "...")]` to `CustomerCrudAppService.CreateAsync` makes `create` appear in the smoke test's customer skill.
6. Adding `[MoAITool(Disabled = true)]` to `PlaceOrderApplicationService.Handle` makes `place-order` disappear.
7. The DTO validation propagation in §7.1 is verified end-to-end: the `PlaceOrderRequestDto`'s `[Required]` and `[MinLength(1)]` annotations appear in the JSON schema.
8. The `Res` / `Res<T>` / `ResPaged<T>` unwrap rules in §9 are verified end-to-end.
9. Solution builds with **zero new warnings**.

## 12. Cross-doc table — what is shared with Doc 03, what differs

| Concern | Doc 03 (Facade Provider) | Doc 04 (ProjectUnit Provider) |
|---|---|---|
| Module home | `Monica.Framework/AISkillProviders/Facade/` | `Monica.Framework/AISkillProviders/ProjectUnit/` |
| Discovery hook | New `IBusinessTypeIterator` filter on `IMonicaFacade` | Reuses `ModuleProjectUnits` registry — no new filter |
| Marker | `IMonicaFacade` interface | None — `ProjectUnit` registration is the marker |
| Skill granularity | One Skill per Facade type | One Skill per ProjectUnit |
| Module Skill aggregator | Yes — one `module-{moduleKey}` Skill referencing per-Facade Skills | No — ProjectUnit is itself the aggregation level |
| Default exposure | All public methods (deny-list applied) | Custom Handle: all; CRUD: read auto, mutating opt-in |
| `[MoAITool]` attribute | Same spec, same priority chain | Same spec, same priority chain |
| `Res<T>` unwrap | Same rule | Same rule (plus `ResPaged<T>` special case) |
| Selective registration | `UseAllFacades` / `UseFacades(Type[])` | `UseAllUnits` / `UseUnits(Type[])` |
| Per-method exclusion | `[MoAITool(Disabled = true)]` only | Same |
| `RequestDto` validation propagation | `[Description]` only on parameters; complex DTOs descend with depth limit | Same + DataAnnotations (`[Required]`, `[StringLength]`, `[Range]`, `[RegularExpression]`, etc.) explicitly mapped |

## 13. Cross-doc references

- Doc 02 (`02-skill-tool-mcp-base.md`) — defines `MoSkill<TSelf>`, `[MoAITool]`, the description-priority chain, the discovery host. Doc 04 normatively references Doc 02 §5, §6.
- Doc 03 (`03-monica-facade-skill-provider.md`) — defines the Facade Provider mechanism. Doc 04 mirrors its structure and cross-references its `Res<T>` unwrap rule (§8) and selective-registration pattern (§5).

---

**End of Doc 04.**
