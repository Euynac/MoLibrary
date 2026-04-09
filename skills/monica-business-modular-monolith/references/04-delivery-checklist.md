# Delivery Checklist

Use this checklist before finishing a modular-monolith architecture change.

- The new bounded context truly deserves its own domain package.
- The solution remains domain-first, not globally layer-first.
- The three shared platform layers are used consistently: `Platform.BuildingBlocks`, `Platform.Infrastructure`, and `Platform.Protocol`.
- Cross-domain dependencies point at `Shared/Platform.Protocol/PublishedLanguages`, not internal implementation projects.
- The `.slnx` solution folders mirror the physical `src/AppHost`, `src/Shared`, and `src/Domains` layout.
- Persistence ownership is clear for the new or changed domain.
- AppHost handlers live in the owning entry project, while domain models, repositories, providers, and persistence stay inside the owning domain package.
- Unit-level implementation follows `monica-project-unit-development`.
