# Delivery Checklist

Use this checklist before finishing a microservice architecture change.

- The target subdomain truly needs its own service boundary.
- The three shared platform layers are used consistently: `BuildingBlocksPlatform`, `InfrastructurePlatform`, and `ProtocolPlatform`.
- `Shared/ProtocolPlatform/PublishedLanguages` contains only stable cross-service contracts.
- The new service has a coherent `API`, `Domain`, and `Infrastructure` split.
- Migration ownership is clear for every new persistence change.
- Cross-service collaboration uses contracts or events instead of direct infrastructure coupling.
- Unit-level implementation follows `monica-project-unit-development`.
