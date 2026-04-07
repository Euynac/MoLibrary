# Boundaries, Dependencies, and Persistence

Use these rules to keep the modular monolith clean instead of turning it into a global layered application.

## Dependency Direction

- `Shared/ProtocolPlatform/PublishedLanguages` may be referenced by other modules and the host.
- `Application` depends on shared protocol contracts and its own `Domain`.
- `Infrastructure` implements the module's persistence and integrations.
- The host composes modules but does not absorb their business logic.

## Shared Platform Responsibilities

- `BuildingBlocksPlatform` is for project-agnostic infrastructure building blocks and third-party framework extensions.
- `InfrastructurePlatform` is for solution-owned infrastructure wiring, environment setup, and project-specific integration composition.
- `ProtocolPlatform` is for the shared business language used across modules.

## Persistence Ownership

- Each module owns its own persistence model, even when multiple modules share the same database engine.
- Keep `DbContext`, repository implementations, and mapping in the owning module's `Infrastructure`.
- Use the central migrator as an operational tool, not as an excuse to centralize domain ownership.

## Anti-Patterns

- Global `Application`, `Domain`, or `Infrastructure` folders containing mixed subdomains
- A separate `{Subdomain}.Contracts` project when `ProtocolPlatform/PublishedLanguages` already owns the shared language
- Cross-module references to another module's repository or persistence implementation
- Host-level handlers that bypass module boundaries
