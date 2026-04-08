# Modular Monolith Solution Layout

Use this structure when the business solution is one deployment but still needs explicit DDD subdomain boundaries.

```text
src/
├── AppHost/
│   ├── Api/
│   │   └── Api.csproj                        # Typical single entry point
│   └── {AnotherEntryPoint}/
│       └── {AnotherEntryPoint}.csproj        # Optional extra entry point
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
│       ├── {Subdomain}.Application/
│       │   ├── HandlersCommand/              # ApplicationService units
│       │   ├── HandlersQuery/                # ApplicationService units
│       │   ├── EventHandlers/                # Event handler units
│       │   └── BackgroundWorkers/            # Job units
│       ├── {Subdomain}.Domain/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   ├── DomainServices/
│       │   ├── Events/
│       │   ├── Interfaces/
│       │   └── Configurations/
│       └── {Subdomain}.Infrastructure/
│           ├── DependencyInjection/          # Domain registration and composition helpers
│           ├── Repository/
│           ├── Persistence/
│           └── Providers/
└── Database/
    └── DbMigrator/
        └── DbMigrator.csproj
```

## Mapping Rules

- Split by subdomain under `Domains/` first. Do not create global `Application`, `Domain`, or `Infrastructure` buckets for the whole solution.
- Keep the same shared platform split across solution styles to reduce learning overhead.
- Put project-agnostic infrastructure extensions into `Shared/Platform.BuildingBlocks/Platform.BuildingBlocks.csproj`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/Platform.Infrastructure/Platform.Infrastructure.csproj`.
- Keep `Shared/Platform.Protocol/PublishedLanguages` stable and referenceable from other domains.
- Keep business implementation inside the owning domain's `Application`, `Domain`, and `Infrastructure` projects.
- Keep AppHost focused on entry-point composition. Do not force `*.WebHost` naming or nested `Modules/Endpoints` folders there.
