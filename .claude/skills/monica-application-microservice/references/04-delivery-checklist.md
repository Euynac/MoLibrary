# Delivery Checklist

Use this checklist before finishing a microservice architecture change.

- The target subdomain truly needs its own service boundary.
- The three shared platform layers are used consistently: `Platform.BuildingBlocks`, `Platform.Infrastructure`, and `Platform.Protocol`.
- The solution-project chain is consistent: `API -> Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- `Shared/Platform.Protocol/PublishedLanguages` contains only stable cross-service contracts.
- Every `ApplicationService` request has one request-owned `[ApiEndpoint]`, while handlers contain no MVC routing or binding attributes.
- Published attributed requests generate the intended `I{Domain}CommandApi`/`I{Domain}QueryApi`; local attributed requests generate controllers only.
- `WebApiGenerationConfig` is present in each request-owning assembly, and no RPC metadata snapshots or build-integrated export tasks remain.
- The `.slnx` solution folders mirror the physical `src/AppHost`, `src/Shared`, `src/Services`, and `src/Migrations` layout.
- The new service has a coherent `API` and `Domain` split.
- AppHost or gateway projects remain composition-only and do not absorb business ProjectUnits.
- `Utilities/` and `Domain/Repository/` are used consistently with the shared ProjectUnit placement rules.
- Migration ownership is clear for every new persistence change.
- Cross-service collaboration uses contracts or events instead of direct infrastructure coupling.
- `monica.AddConfiguration()` is part of the host-bound module graph; bootstrap composition reads `builder.Configuration`, and runtime consumers use typed options injection.
- Unit-level implementation follows `monica-application-project-unit-development`.
