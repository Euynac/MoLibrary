# Delivery Checklist

Use this checklist before finishing a modular-monolith architecture change.

- The new bounded context truly deserves its own module.
- The solution remains module-first, not globally layer-first.
- The three shared platform layers are used consistently: `BuildingBlocksPlatform`, `InfrastructurePlatform`, and `ProtocolPlatform`.
- Cross-module dependencies point at `Shared/ProtocolPlatform/PublishedLanguages`, not internal implementation projects.
- Persistence ownership is clear for the new or changed module.
- Host composition stays thin and does not absorb business logic.
- Unit-level implementation follows `monica-project-unit-development`.
