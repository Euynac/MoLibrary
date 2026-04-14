# Modular Monolith Solution Layout

Use this structure when the business solution is one deployment but still needs explicit DDD subdomain boundaries.

```text
src/
├── AppHost/
│   └── {ProjectName}/
│       ├── {ProjectName}.csproj              # Entry point, prefer explicit names such as Monica.Docs.Api
│       └── Program.cs                        # Composition root only
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
│       ├── Application/                      # Domain-owned use cases and background workflows
│       │   ├── HandlersCommand/              # ApplicationService command units
│       │   ├── HandlersQuery/                # ApplicationService query units
│       │   ├── HandlersEvent/                # Event handler units
│       │   └── BackgroundWorkers/            # Job units
│       ├── Entities/
│       ├── ValueObjects/
│       ├── Events/
│       ├── Interfaces/
│       ├── DomainServices/
│       ├── Configurations/
│       ├── Utilities/                       # Pure helper code, named `Utils*`
│       └── Repository/                      # Repository implementations, DbContext, and EF mapping
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
- Use the strict solution-project reference chain `AppHost -> Domains.{Subdomain} -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Put project-agnostic infrastructure extensions into `Shared/Platform.BuildingBlocks/Platform.BuildingBlocks.csproj`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/Platform.Infrastructure/Platform.Infrastructure.csproj`.
- Keep `Shared/Platform.Protocol/PublishedLanguages` stable and referenceable from other domains.
- Keep subdomain-specific package references in the owning `Domains.{Subdomain}.csproj`; do not introduce extra shared-project references just to reach them.
- Keep domain models, repositories, `DbContext` ownership, and helper code inside the owning `Domains.{Subdomain}.csproj`.
- Put application services, event handlers, and background workers in the owning domain package under `Application/HandlersCommand`, `Application/HandlersQuery`, `Application/HandlersEvent`, and `Application/BackgroundWorkers`.
- Keep `DomainServices/` as the standard folder for `DomainService` units.
- Keep `Utilities/` as the standard folder for pure helpers and name them `Utils*`.
- Do not create `Persistence/` or `Providers/` as default folders. Keep repository implementations, `DbContext`, and EF mapping in `Repository/`.
- Keep AppHost entry projects down to `{ProjectName}.csproj` and `Program.cs`. Do not place business ProjectUnits there.
- Keep `.slnx` folders aligned with the real `src/` tree so the solution view matches the layout rules.
- Do not force `*.WebHost` naming or nested `Modules/Endpoints` folders inside AppHost.
