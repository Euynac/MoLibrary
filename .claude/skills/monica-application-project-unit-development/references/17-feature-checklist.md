# Feature Checklist

Use this checklist before finishing a ProjectUnit change.

- The chosen ProjectUnits match the feature shape in [02-project-unit-composition-map.md](02-project-unit-composition-map.md).
- Class names follow the conventions in [01-project-unit-naming-and-boundaries.md](01-project-unit-naming-and-boundaries.md).
- The physical folders match the placement matrix in [01-project-unit-naming-and-boundaries.md](01-project-unit-naming-and-boundaries.md).
- `ApplicationService` remains thin and returns `Res` only at the boundary.
- Business rules live on entities or in `DomainService`, not in adapters.
- Contracts are separate from persistence entities.
- Every generated HTTP endpoint declares `[ApiEndpoint]` on its request, while its handler contains no MVC endpoint or binding attributes.
- Published RPC requests use the matching `PublishedLanguages.Domain{DomainName}.Requests` namespace and protocol `WebApiGenerationConfig`; local HTTP requests remain outside the published namespace.
- Repository interfaces and implementations live on the correct side of the boundary.
- `DbContext`, EF mapping, and utility helpers use the expected folders and naming conventions.
- New events, jobs, and options exist only because the feature genuinely needs them.
- The entry project registers `monica.AddConfiguration()` inside `builder.AddMonica(...)`; bootstrap composition reads `builder.Configuration` directly, while runtime code consumes the ProjectUnit through typed options injection.
- The selected architecture skill still agrees with the physical folder and project placement, including AppHost or gateway composition-only rules.
