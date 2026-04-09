# Boundaries, Dependencies, and Persistence

Use these rules to keep the modular monolith clean instead of turning it into a global layered application.

## Dependency Direction

- `Shared/Platform.Protocol/PublishedLanguages` may be referenced by other domains and AppHost entry projects.
- Domain-owned `Application` units depend on shared protocol contracts and their own domain package.
- `Domains.{Subdomain}.csproj` owns the domain's persistence and integrations.
- AppHost entry projects compose domains, but they should not absorb handlers, jobs, domain models, repositories, or providers.

## Shared Platform Responsibilities

- `Platform.BuildingBlocks` is for project-agnostic infrastructure building blocks and third-party framework extensions.
- `Platform.Infrastructure` is for solution-owned infrastructure wiring, environment setup, and project-specific integration composition.
- `Platform.Protocol` is for the shared business language used across domains.

## Persistence Ownership

- Each domain owns its own persistence model, even when multiple domains share the same database engine.
- Keep `DbContext`, repository implementations, and mapping in the owning `Domains.{Subdomain}.csproj`.
- Use the central migrator as an operational tool, not as an excuse to centralize domain ownership.

## Anti-Patterns

- Global `Application`, `Domain`, or `Infrastructure` folders containing mixed subdomains
- Separate `{Subdomain}.Application`, `{Subdomain}.Domain`, and `{Subdomain}.Infrastructure` projects when this layout already uses a merged domain package
- A separate `{Subdomain}.Contracts` project when `Platform.Protocol/PublishedLanguages` already owns the shared language
- Cross-domain references to another domain's repository or persistence implementation
- AppHost entry projects that grow business handlers or jobs instead of staying composition-only
- Cross-domain references to another domain's `Application` handlers or internals
- `.slnx` files that flatten projects and ignore the `src/AppHost`, `src/Shared`, `src/Domains` layout
