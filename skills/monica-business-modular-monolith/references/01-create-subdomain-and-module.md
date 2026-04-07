# Create a New Subdomain Module

Use this workflow when a new bounded context should live inside the same deployable application.

## Step 1. Name the bounded context

- Choose the business name first, such as `Ordering`, `Billing`, or `Warehouse`.
- Use that name consistently for the shared language area and all three module projects:
  - `Domain{Subdomain}` in `ProtocolPlatform/PublishedLanguages`
  - `{Subdomain}.Application`
  - `{Subdomain}.Domain`
  - `{Subdomain}.Infrastructure`

## Step 2. Create the protocol language area

Create:

```text
src/Shared/ProtocolPlatform/PublishedLanguages/Domain{Subdomain}/
├── Requests/
├── Models/
├── Events/
└── AppInterfaces/    # only if synchronous module-to-module calls are required
```

Rules:

- Put only stable cross-module requests, DTOs, enums, events, and optional service interfaces here.
- Keep persistence entities and repositories out of `ProtocolPlatform`.

## Step 3. Create the module package

Create:

```text
src/Modules/{Subdomain}/
├── {Subdomain}.Application/
├── {Subdomain}.Domain/
└── {Subdomain}.Infrastructure/
```

Use `monica-project-unit-development` to place the correct ProjectUnits inside these projects.

## Step 4. Register the module

- Add module registration and host composition in the application host.
- Keep host startup thin; the module still owns its behavior internally.

## Step 5. Add persistence ownership

- Add or extend the module's `Persistence` area and the shared migrator entry point.
- Keep ownership explicit even though deployment is shared.

## Decision Rule

Create a new module only when the subdomain has distinct language, invariants, and ownership. Do not create a module for every page or CRUD screen.
