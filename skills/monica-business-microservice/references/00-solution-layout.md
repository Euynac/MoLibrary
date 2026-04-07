# Microservice Solution Layout

Use this structure when a Monica business solution is intentionally split into independently owned subdomain services.

```text
src/
├── AppHost/                                  # Optional orchestration/composition host
├── Shared/
│   ├── BuildingBlocksPlatform/               # Project-agnostic infrastructure building blocks and third-party extensions
│   ├── InfrastructurePlatform/               # Solution-owned infrastructure setup and integration wiring
│   └── ProtocolPlatform/
│       └── PublishedLanguages/
│           └── Domain{Subdomain}/
│               ├── Requests/                 # Shared request contracts
│               ├── Models/                   # Shared DTOs and enums
│               ├── Events/                   # Shared event contracts
│               └── AppInterfaces/            # Optional synchronous service contracts
├── Services/
│   └── {Subdomain}/
│       ├── {Subdomain}Service.API/
│       │   ├── Modules/                      # Service registration and composition
│       │   ├── HandlersCommand/              # ApplicationService units
│       │   ├── HandlersQuery/                # ApplicationService units
│       │   ├── HandlersEvent/                # Event handler units
│       │   ├── BackgroundWorkers/            # Job units
│       │   ├── ServicesHttp/                 # HTTP adapters only
│       │   └── ServicesGrpc/                 # gRPC adapters only
│       ├── {Subdomain}Service.Domain/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   ├── DomainServices/
│       │   ├── Events/
│       │   ├── Interfaces/
│       │   └── Configurations/
│       └── {Subdomain}Service.Infrastructure/
│           ├── Repository/
│           ├── Persistence/
│           └── Providers/
└── Migrations/
    └── {Subdomain}/                          # Migration project per service
```

## Mapping Rules

- Keep the same shared platform split across solution styles to reduce learning overhead.
- Put project-agnostic infrastructure extensions into `Shared/BuildingBlocksPlatform`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/InfrastructurePlatform`.
- Put cross-service contracts in `Shared/ProtocolPlatform/PublishedLanguages`, not inside a service project.
- Put business implementation in the service's own `API`, `Domain`, and `Infrastructure` projects.
- Put migrations beside the service boundary they belong to, not in a global dump folder.
- Keep `ServicesHttp` and `ServicesGrpc` as adapters. Core business logic still lives in shared ProjectUnits.
