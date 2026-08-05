# Delivery Checklist

Use this checklist before finishing a modular-monolith architecture change.

- The new bounded context truly deserves its own domain package.
- The solution remains domain-first, not globally layer-first.
- The three shared platform layers are used consistently: `Platform.BuildingBlocks`, `Platform.Infrastructure`, and `Platform.Protocol`.
- The solution-project chain is consistent: `AppHost -> Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Cross-domain dependencies point at `Shared/Platform.Protocol/PublishedLanguages`, not internal implementation projects.
- Every `ApplicationService` request owns one `[ApiEndpoint]`; handlers have no route, verb, or binding attributes.
- Published attributed requests generate the intended `I{Domain}CommandApi`/`I{Domain}QueryApi`, while local attributed requests generate controllers only.
- Each request-owning assembly has valid `WebApiGenerationConfig`, and no RPC metadata snapshots or export tasks remain.
- The `.slnx` solution folders mirror the physical `src/AppHost`, `src/Shared`, and `src/Domains` layout.
- Persistence ownership is clear for the new or changed domain.
- AppHost stays as a Program-only composition entry point.
- Domain-owned application units live under `Application/HandlersCommand`, `Application/HandlersQuery`, `Application/HandlersEvent`, and `Application/BackgroundWorkers`.
- Domain models, `DomainServices`, `Utilities`, repositories, and `DbContext`-related files stay inside the owning domain package.
- `monica.AddConfiguration()` is part of the host-bound module graph; bootstrap composition reads `builder.Configuration`, and runtime consumers use typed options injection.
- Unit-level implementation follows `monica-application-project-unit-development`.
