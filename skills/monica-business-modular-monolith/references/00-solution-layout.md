# Modular Monolith Solution Layout

Use this structure when the business solution is one deployment but still needs explicit DDD subdomain boundaries.

```text
src/
├── AppHost/
│   └── {ProjectName}/
│       ├── {ProjectName}.csproj              # Entry point, prefer explicit names such as Monica.Docs.Api
│       ├── HandlersCommand/                  # ApplicationService units for this entry surface
│       ├── HandlersQuery/                    # ApplicationService units for this entry surface
│       ├── EventHandlers/                    # Event handler units for this entry surface
│       └── BackgroundWorkers/                # Job units for this entry surface
├── Shared/
│   ├── Platform.BuildingBlocks/
│   │   └── Platform.BuildingBlocks.csproj    # Project-agnostic infrastructure building blocks and third-party extensions
│   ├── Platform.Infrastructure/
│   │   └── Platform.Infrastructure.csproj    # Solution-owned infrastructure setup and integration wiring
│   └── Platform.Protocol/
│       ├── Platform.Protocol.csproj
│       └── PublishedLanguages/
│           └── Domain{Subdomain}/
│               ├── Requests/                 # Shared request contracts
│               ├── Models/                   # Stable DTOs and enums
│               ├── Events/                   # Cross-domain event contracts
│               └── AppInterfaces/            # Optional synchronous domain contracts
├── Domains/
│   └── {Subdomain}/
│       ├── Domains.{Subdomain}.csproj        # Single domain package project
│       ├── Entities/
│       ├── ValueObjects/
│       ├── Events/
│       ├── Interfaces/
│       ├── Services/
│       ├── Configurations/
│       ├── DependencyInjection/              # Domain registration and composition helpers
│       ├── Repository/
│       ├── Persistence/
│       └── Providers/
└── Database/
    └── DbMigrator/
        └── DbMigrator.csproj
```

Recommended `.slnx` folders should mirror the physical layout:

```text
/src/AppHost/
/src/Shared/
/src/Domains/
```

## Mapping Rules

- Split by subdomain under `Domains/` first. Do not create global `Application`, `Domain`, or `Infrastructure` buckets for the whole solution.
- Keep the same shared platform split across solution styles to reduce learning overhead.
- Put project-agnostic infrastructure extensions into `Shared/Platform.BuildingBlocks/Platform.BuildingBlocks.csproj`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/Platform.Infrastructure/Platform.Infrastructure.csproj`.
- Keep `Shared/Platform.Protocol/PublishedLanguages` stable and referenceable from other domains.
- Keep domain models, repositories, providers, and persistence ownership inside the owning `Domains.{Subdomain}.csproj`.
- Put host-facing handlers in the owning AppHost entry project under `HandlersCommand`, `HandlersQuery`, `EventHandlers`, and `BackgroundWorkers`.
- Keep `.slnx` folders aligned with the real `src/` tree so the solution view matches the layout rules.
- Do not force `*.WebHost` naming or nested `Modules/Endpoints` folders inside AppHost.
