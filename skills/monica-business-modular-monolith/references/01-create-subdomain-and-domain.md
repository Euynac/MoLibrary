# Create a New Subdomain Domain Package

Use this workflow when a new bounded context should live inside the same deployable application.

## Step 1. Name the bounded context

- Choose the business name first, such as `Ordering`, `Billing`, or `Warehouse`.
- Use that name consistently for the shared language area and all three domain projects:
  - `Domain{Subdomain}` in `Platform.Protocol/PublishedLanguages`
  - `{Subdomain}.Application`
  - `{Subdomain}.Domain`
  - `{Subdomain}.Infrastructure`

## Step 2. Create the protocol language area

Create:

```text
src/Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/
├── Requests/
├── Models/
├── Events/
└── AppInterfaces/    # only if synchronous domain-to-domain calls are required
```

Rules:

- Put only stable cross-domain requests, DTOs, enums, events, and optional service interfaces here.
- Keep persistence entities and repositories out of `Platform.Protocol`.

## Step 3. Create the domain package

Create:

```text
src/Domains/{Subdomain}/
├── {Subdomain}.Application/
├── {Subdomain}.Domain/
└── {Subdomain}.Infrastructure/
```

Use `monica-project-unit-development` to place the correct ProjectUnits inside these projects.

## Step 4. Register the domain

- Add domain registration and host composition in an AppHost entry project, typically `AppHost/Api`.
- Keep host startup thin; the domain still owns its behavior internally.

## Step 5. Add persistence ownership

- Add or extend the domain's `Persistence` area and the shared migrator entry point.
- Keep ownership explicit even though deployment is shared.

## Decision Rule

Create a new domain package only when the subdomain has distinct language, invariants, and ownership. Do not create one for every page or CRUD screen.
