# Protocol Platform and Internal Collaboration

Use this file before introducing a new dependency between modules.

## Default Rule

Other modules may depend on `Shared/ProtocolPlatform/PublishedLanguages` and optional `AppInterfaces`, not on another module's `Application` or `Infrastructure`.

## What Belongs in ProtocolPlatform

- Requests
- DTOs
- Enums
- Cross-module events
- Optional `AppInterfaces` for deliberate synchronous collaboration

## What Stays Internal

- Entities
- Repositories
- EF configuration
- Infrastructure providers
- Internal domain services and implementation details

## Collaboration Choices

- Use direct contract-driven requests when another module needs synchronous data or command execution.
- Use events when the collaboration can be asynchronous and loosely coupled.
- Keep collaboration intentional. A modular monolith should not simulate service calls for everything, but it should still preserve module boundaries.
