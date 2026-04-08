# Delivery Checklist

Use this checklist before finishing a modular-monolith architecture change.

- The new bounded context truly deserves its own domain package.
- The solution remains domain-first, not globally layer-first.
- The three shared platform layers are used consistently: `Platform.BuildingBlocks`, `Platform.Infrastructure`, and `Platform.Protocol`.
- Cross-domain dependencies point at `Shared/Platform.Protocol/PublishedLanguages`, not internal implementation projects.
- Persistence ownership is clear for the new or changed domain.
- AppHost composition stays thin and does not absorb business logic.
- Unit-level implementation follows `monica-project-unit-development`.
