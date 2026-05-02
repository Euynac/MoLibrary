# Platform Protocol and Internal Collaboration

Use this file before introducing a new dependency between domains.

## Default Rule

Other domains may depend on `Shared/Platform.Protocol/PublishedLanguages`, shared `Contracts/`, and only the deliberate `Implementations/*` surfaces exposed there, not on another domain's internal implementation or another domain's `Application` handlers.

Within solution-project references, use the chain `Domains.{Subdomain} -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`. Do not make domains jump directly to `Platform.Protocol` or `Platform.BuildingBlocks`.

## What Belongs in Platform.Protocol

- Requests
- DTOs
- Enums
- Cross-domain events
- Optional `Contracts/` for deliberate synchronous collaboration
- Optional `Implementations/Local` for checked-in local or actor-backed providers

## What Stays Internal

- Entities
- Repositories
- EF configuration
- Infrastructure adapters
- Domain-owned application handlers and background workers
- Internal domain services and implementation details

## Collaboration Choices

- Use direct contract-driven requests when another domain needs synchronous data or command execution.
- Use events when the collaboration can be asynchronous and loosely coupled.
- When RPC generation is enabled, generated HTTP clients may surface as `Implementations.Http`; only check in `Implementations/Http` source when intentionally wrapping or customizing that client surface.
- Keep collaboration intentional. A modular monolith should not simulate service calls for everything, but it should still preserve domain boundaries.
