---
name: monica-application
description: Router skill for Monica application development. Use when designing or implementing Monica-based DDD applications, choosing between microservice and modular monolith architecture, or creating ProjectUnit-based application features.
---

# Monica Application

Use this skill as the entry point for products and application systems built on Monica. It routes solution-level architecture work and unit-level feature implementation to the correct granular skill.

## Routing

- Microservice solution layout, service boundaries, `Platform.Protocol`, published language, migrations, or cross-service collaboration: use `$monica-application-microservice`.
- Modular monolith layout, bounded contexts under `Domains/`, AppHost composition, cross-domain collaboration, or persistence ownership: use `$monica-application-modular-monolith`.
- Feature implementation with `ApplicationService`, `RequestDto`, `DomainService`, `Entity`, `Repository`, events, configuration, recurring jobs, or triggered jobs: use `$monica-application-project-unit-development`.

## Working Rule

Start with the architecture skill when the deployment style or subdomain placement is unclear. Use `$monica-application-project-unit-development` once the target project and boundary are known.
