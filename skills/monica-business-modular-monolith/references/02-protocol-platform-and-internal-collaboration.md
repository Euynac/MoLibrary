# Platform Protocol and Internal Collaboration

Use this file before introducing a new dependency between domains.

## Default Rule

Other domains may depend on `Shared/Platform.Protocol/PublishedLanguages` and optional `AppInterfaces`, not on another domain's `Application` or `Infrastructure`.

## What Belongs in Platform.Protocol

- Requests
- DTOs
- Enums
- Cross-domain events
- Optional `AppInterfaces` for deliberate synchronous collaboration

## What Stays Internal

- Entities
- Repositories
- EF configuration
- Infrastructure providers
- Internal domain services and implementation details

## Collaboration Choices

- Use direct contract-driven requests when another domain needs synchronous data or command execution.
- Use events when the collaboration can be asynchronous and loosely coupled.
- Keep collaboration intentional. A modular monolith should not simulate service calls for everything, but it should still preserve domain boundaries.
