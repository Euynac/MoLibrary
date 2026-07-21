# Platform Protocol and Service Contracts

Use this file before adding anything to `Shared/Platform.Protocol`.

## Shared Platform Responsibilities

- `Platform.BuildingBlocks` contains project-agnostic infrastructure building blocks and third-party framework extensions.
- `Platform.Infrastructure` contains solution-owned infrastructure setup and integration wiring.
- `Platform.Protocol` contains the shared business language used across services.

Within solution-project references, use the chain `{Subdomain}Service.API -> {Subdomain}Service.Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`. Do not make service projects jump directly to `Platform.Protocol` or `Platform.BuildingBlocks`.

## What Belongs Here

- Put requests that another service or shared gateway may invoke into the strict `Platform.Protocol/PublishedLanguages/Domain{Domain}/Requests` namespace.
- Add `[ApiEndpoint]` to a published `Command*` or `Query*` only when it is an HTTP/RPC operation. Its XML summary, verb, relative route, binding, and optional `OperationName` are the source of truth.
- Use `Res`, `Res<T>`, `ResPaged<T>`, or a custom `IRemoteResultEnvelope<TSelf>` for published RPC results. A custom envelope must create transport failures through its normal constructor in `CreateRemoteFailure`.
- Put DTOs and enums that form part of a stable service contract into `Platform.Protocol/PublishedLanguages`.
- Put events that other services may subscribe to into `Platform.Protocol/PublishedLanguages`.
- Put optional checked-in synchronous abstractions into `Platform.Protocol/PublishedLanguages/.../Contracts`.
- Put optional checked-in local or actor-backed providers into `Platform.Protocol/PublishedLanguages/.../Implementations/Local`.

## What Does Not Belong Here

- Persistence entities
- EF configuration
- Repository abstractions
- Internal-only domain objects
- Transport-only controller or endpoint models that are not part of the service contract
- Multipart, download, raw-object, and other HTTP-only request shapes; co-locate these with their service handlers instead

## Collaboration Defaults

- Prefer async collaboration through events when eventual consistency is acceptable.
- Prefer direct service contracts only when the caller truly needs synchronous data or command execution.
- Generated contracts are named `I{Domain}CommandApi` and `I{Domain}QueryApi`, with `Http*` and/or `Local*` implementations selected by the protocol assembly's `WebApiGenerationConfig`. Do not check in parallel generated surfaces.
- Keep `PublishedLanguages` narrow. If a contract is only used inside one service, keep it local.
- Do not use metadata JSON, `AdditionalFiles`, bootstrap scans, or source-tree writes to discover RPC operations; attributed published source is the contract.

## Stability Rules

- Change published contracts deliberately. They are not internal implementation details.
- Prefer additive evolution over rewriting contract meaning in place.
- Keep payloads explicit and serializable.
