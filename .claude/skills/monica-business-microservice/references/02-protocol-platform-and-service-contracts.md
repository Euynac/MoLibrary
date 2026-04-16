# Platform Protocol and Service Contracts

Use this file before adding anything to `Shared/Platform.Protocol`.

## Shared Platform Responsibilities

- `Platform.BuildingBlocks` contains project-agnostic infrastructure building blocks and third-party framework extensions.
- `Platform.Infrastructure` contains solution-owned infrastructure setup and integration wiring.
- `Platform.Protocol` contains the shared business language used across services.

Within solution-project references, use the chain `{Subdomain}Service.API -> {Subdomain}Service.Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`. Do not make service projects jump directly to `Platform.Protocol` or `Platform.BuildingBlocks`.

## What Belongs Here

- Put requests that another service or shared gateway may invoke into `Platform.Protocol/PublishedLanguages`.
- Put DTOs and enums that form part of a stable service contract into `Platform.Protocol/PublishedLanguages`.
- Put events that other services may subscribe to into `Platform.Protocol/PublishedLanguages`.
- Put optional `AppInterfaces` into `Platform.Protocol/PublishedLanguages` only when direct synchronous collaboration is justified.

## What Does Not Belong Here

- Persistence entities
- EF configuration
- Repository abstractions
- Internal-only domain objects
- Transport-only controller or endpoint models that are not part of the service contract

## Collaboration Defaults

- Prefer async collaboration through events when eventual consistency is acceptable.
- Prefer direct service contracts only when the caller truly needs synchronous data or command execution.
- Keep `PublishedLanguages` narrow. If a contract is only used inside one service, keep it local.

## Stability Rules

- Change published contracts deliberately. They are not internal implementation details.
- Prefer additive evolution over rewriting contract meaning in place.
- Keep payloads explicit and serializable.
