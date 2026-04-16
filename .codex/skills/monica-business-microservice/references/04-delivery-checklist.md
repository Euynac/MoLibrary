# Delivery Checklist

Use this checklist before finishing a microservice architecture change.

- The target subdomain truly needs its own service boundary.
- The three shared platform layers are used consistently: `Platform.BuildingBlocks`, `Platform.Infrastructure`, and `Platform.Protocol`.
- The solution-project chain is consistent: `API -> Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- `Shared/Platform.Protocol/PublishedLanguages` contains only stable cross-service contracts.
- The `.slnx` solution folders mirror the physical `src/AppHost`, `src/Shared`, `src/Services`, and `src/Migrations` layout.
- The new service has a coherent `API` and `Domain` split.
- AppHost or gateway projects remain composition-only and do not absorb business ProjectUnits.
- `Utilities/` and `Domain/Repository/` are used consistently with the shared ProjectUnit placement rules.
- Migration ownership is clear for every new persistence change.
- Cross-service collaboration uses contracts or events instead of direct infrastructure coupling.
- Unit-level implementation follows `monica-project-unit-development`.
