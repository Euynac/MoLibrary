# Service Boundaries and Collaboration

Use these rules to keep the microservice architecture from collapsing into a distributed monolith.

## Ownership

- One subdomain service owns one business capability area and its persistence model.
- A service may expose contracts, but it does not expose its internal repository or infrastructure code.
- A service should not read or write another service's tables directly.

## Dependency Direction

- `Shared/BuildingBlocksPlatform` may be referenced where generic infrastructure building blocks are needed.
- `Shared/InfrastructurePlatform` may be referenced by host and infrastructure composition code, but it is not a business-language surface.
- `Shared/ProtocolPlatform/PublishedLanguages` may be referenced broadly.
- A service's `Domain` and `Infrastructure` stay private to that service.
- Cross-service collaboration happens through published requests, models, events, or explicitly chosen service interfaces.

## Practical Boundaries

- Use a new service when the business capability has distinct language, lifecycle, or data ownership.
- Stay within the existing service when the new feature is just another use case inside the same subdomain.
- Avoid splitting a service solely by CRUD screens, endpoint count, or team preference.

## Adapter Rule

- `ServicesHttp`, `ServicesGrpc`, and host modules are delivery mechanisms.
- They do not replace `ApplicationService`, `DomainService`, `Entity`, `Repository`, or other ProjectUnits.
