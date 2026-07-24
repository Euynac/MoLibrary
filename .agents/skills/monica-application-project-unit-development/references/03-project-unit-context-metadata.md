# ProjectUnit Context Metadata

Use this reference before creating or changing any discovered ProjectUnit. The annotations form the agent-readable architecture catalog shown by Monica's status dashboard and typed APIs.

## Required Decisions

Resolve these facts from repository conventions and source requirements before writing code:

- `$Owner$`: the team, role, or capability accountable for the unit.
- `$RequirementId$`: a stable requirement identifier owned by the application or its workflow system.
- `$SubdomainTag$`: the bounded context or subdomain tag.
- `$FeatureTag$`: the capability or feature tag.
- A concise title and one-sentence responsibility specific to this unit.

If any fact cannot be discovered, ask one focused question. Do not invent ownership or requirement IDs.

## Annotation Pattern

```csharp
using Monica.ProjectUnits.Annotations;

[ProjectUnitMetadata(
    "Approve Order",
    Owner = "$Owner$",
    Description = "Approves an eligible order.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class CommandHandlerApproveOrder : ApplicationService<CommandApproveOrder>
{
    // Implementation omitted.
}
```

Apply the attributes directly to every discovered class or record. Metadata does not flow from a base class. Add another `[ProjectUnitRequirement("...")]` for each independently traceable requirement.

## Normalization And Diagnostics

- Monica trims titles, owners, descriptions, tags, and requirement IDs.
- Duplicate tags and requirement IDs are removed case-insensitively.
- Monica does not validate a project-specific requirement ID pattern.
- Missing annotations are coverage debt, not startup failures.
- Explicit but empty titles, owners, tags, or requirement IDs produce catalog warnings.
- Keep titles concise because Serilog source enrichment and management UI use the normalized title.

## Requirement Navigation

Requirement links are host-owned. A host may implement `IProjectUnitRequirementLinkResolver` and register it with `UseRequirementLinkResolver<TResolver>()`. The resolver runs only when detail is requested; unresolved IDs remain visible and non-clickable.

Do not put paths or URLs into the annotation. Store stable IDs and let the host resolver map them to the current documentation system.
