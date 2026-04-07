# Modular Monolith Solution Layout

Use this structure when the business solution is one deployment but still needs explicit DDD subdomain boundaries.

```text
src/
├── AppHost/
│   └── {Solution}.WebHost/
│       ├── Modules/                          # Composition root only
│       └── Endpoints/                        # Optional host-level adapters
├── Shared/
│   ├── BuildingBlocksPlatform/               # Project-agnostic infrastructure building blocks and third-party extensions
│   ├── InfrastructurePlatform/               # Solution-owned infrastructure setup and integration wiring
│   └── ProtocolPlatform/
│       └── PublishedLanguages/
│           └── Domain{Subdomain}/
│               ├── Requests/                 # Shared request contracts
│               ├── Models/                   # Stable DTOs and enums
│               ├── Events/                   # Cross-module event contracts
│               └── AppInterfaces/            # Optional synchronous module contracts
├── Modules/
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
│           ├── Modules/                      # Module registration
│           ├── Repository/
│           ├── Persistence/
│           └── Providers/
└── Database/
    └── {Solution}.DbMigrator/
```

## Mapping Rules

- Split by subdomain module first. Do not create global `Application`, `Domain`, or `Infrastructure` buckets for the whole solution.
- Keep the same shared platform split across solution styles to reduce learning overhead.
- Put project-agnostic infrastructure extensions into `Shared/BuildingBlocksPlatform`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/InfrastructurePlatform`.
- Keep `Shared/ProtocolPlatform/PublishedLanguages` stable and referenceable from other modules.
- Keep business implementation inside the owning module's `Application`, `Domain`, and `Infrastructure` projects.
- Keep the host project focused on composition.
