# Feature Checklist

Use this checklist before finishing a ProjectUnit change.

- The chosen ProjectUnits match the feature shape in [02-project-unit-composition-map.md](02-project-unit-composition-map.md).
- Class names follow the conventions in [01-project-unit-naming-and-boundaries.md](01-project-unit-naming-and-boundaries.md).
- The physical folders match the placement matrix in [01-project-unit-naming-and-boundaries.md](01-project-unit-naming-and-boundaries.md).
- `ApplicationService` remains thin and returns `Res` only at the boundary.
- Business rules live on entities or in `DomainService`, not in adapters.
- Contracts are separate from persistence entities.
- Repository interfaces and implementations live on the correct side of the boundary.
- `DbContext`, EF mapping, and utility helpers use the expected folders and naming conventions.
- New events, jobs, and options exist only because the feature genuinely needs them.
- If a `Configuration` unit is consumed during registration or bootstrap, the entry project uses `Mo.AddConfiguration(...)` followed by `Mo.RegisterInstantly(builder)` before dependent registrations.
- The selected architecture skill still agrees with the physical folder and project placement, including AppHost or gateway composition-only rules.
