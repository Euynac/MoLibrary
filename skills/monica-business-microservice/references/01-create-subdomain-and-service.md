# Create a New Subdomain Service

Use this workflow when adding a new business capability that deserves its own service boundary.

## Step 1. Name the subdomain

- Choose a business name first, such as `Ordering`, `Billing`, or `Warehouse`.
- Use that name consistently for:
  - `Domain{Subdomain}` in `ProtocolPlatform/PublishedLanguages`
  - `{Subdomain}Service.API`
  - `{Subdomain}Service.Domain`
  - `{Subdomain}Service.Infrastructure`
  - `Migrations/{Subdomain}`

## Step 2. Create the shared contract area

Create:

```text
src/Shared/ProtocolPlatform/PublishedLanguages/Domain{Subdomain}/
├── Requests/
├── Models/
├── Events/
└── AppInterfaces/    # only if synchronous service-to-service calls are required
```

Rules:

- Put only stable cross-service contracts here.
- Do not copy entity types into this area.

## Step 3. Create the service trio

Create:

```text
src/Services/{Subdomain}/
├── {Subdomain}Service.API/
├── {Subdomain}Service.Domain/
└── {Subdomain}Service.Infrastructure/
```

Use `monica-project-unit-development` to fill the correct ProjectUnits inside those projects.

## Step 4. Add persistence ownership

- If the new service owns data, create or extend its migration project under `src/Migrations/{Subdomain}/`.
- Keep each service responsible for its own persistence model and migrations.

## Step 5. Wire the host

- Register the service in the solution's host or orchestration layer.
- Keep orchestration metadata outside the domain projects.

## Decision Rule

Create a new service only when the subdomain owns distinct business capability and data. Do not create a service just because a new HTTP endpoint appeared.
