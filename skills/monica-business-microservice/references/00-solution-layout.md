# Microservice Solution Layout

Use this structure when a Monica business solution is intentionally split into independently owned subdomain services.

```text
src/
├── AppHost/                                  # Optional orchestration/composition entry points
│   └── Gateway/
│       ├── Gateway.csproj
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
│               ├── Models/                   # Shared DTOs and enums
│               ├── Events/                   # Shared event contracts
│               └── AppInterfaces/            # Optional synchronous service contracts
├── Services/
│   └── {Subdomain}/
│       ├── {Subdomain}Service.API/
│       │   ├── HandlersCommand/              # ApplicationService units
│       │   ├── HandlersQuery/                # ApplicationService units
│       │   ├── HandlersEvent/                # Event handler units
│       │   └── BackgroundWorkers/            # Job units
│       ├── {Subdomain}Service.Domain/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   ├── DomainServices/
│       │   ├── Events/
│       │   ├── Interfaces/
│       │   ├── Configurations/
│       │   ├── Utilities/                   # Pure helper code, named `Utils*`
│       │   └── Repository/                  # Repository implementations, DbContext, and EF mapping
└── Migrations/
    └── {Subdomain}/                          # Migration project per service
```

Recommended `.slnx` folders should mirror the physical layout:

```text
/src/AppHost/
/src/Shared/
/src/Services/
/src/Migrations/
```

## Mapping Rules

- Keep the same shared platform split across solution styles to reduce learning overhead.
- Use the strict solution-project reference chain `{Subdomain}Service.API -> {Subdomain}Service.Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Put project-agnostic infrastructure extensions into `Shared/Platform.BuildingBlocks/Platform.BuildingBlocks.csproj`.
- Put solution-owned infrastructure composition and integration configuration into `Shared/Platform.Infrastructure/Platform.Infrastructure.csproj`.
- Put cross-service contracts in `Shared/Platform.Protocol/PublishedLanguages`, not inside a service project.
- Put business implementation in the service's own `API` and `Domain` projects.
- Keep service-only package references in `{Subdomain}Service.Domain`. Do not add extra shared-project references just to reach them.
- Put `ApplicationService`, event-handler, and background-worker ProjectUnits in the service `API` project under `HandlersCommand`, `HandlersQuery`, `HandlersEvent`, and `BackgroundWorkers`.
- Keep `DomainServices/` as the standard folder for `DomainService` units.
- Keep `Utilities/` in the `Domain` project for pure helpers and name them `Utils*`.
- Do not create `Persistence/` or `Providers/` as default folders. Keep repository implementations, `DbContext`, and EF mapping in `Domain/Repository/`.
- Put migrations beside the service boundary they belong to, not in a global dump folder.
- Keep AppHost or gateway entry projects down to the project file and `Program.cs`. Do not place business ProjectUnits there.
- Keep `.slnx` folders aligned with the real `src/` tree so the solution view reflects AppHost, shared platform, services, and migrations ownership correctly.
