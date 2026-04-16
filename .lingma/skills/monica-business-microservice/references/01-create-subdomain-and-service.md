# Create a New Subdomain Service

Use this workflow when adding a new business capability that deserves its own service boundary.

## Step 1. Name the subdomain

- Choose a business name first, such as `Ordering`, `Billing`, or `Warehouse`.
- Use that name consistently for:
  - `Domain{Subdomain}` in `Platform.Protocol/PublishedLanguages`
  - `{Subdomain}Service.API`
  - `{Subdomain}Service.Domain`
  - `Migrations/{Subdomain}`

## Step 2. Create the shared contract area

Create:

```text
src/Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/
├── Requests/
├── Models/
├── Events/
└── AppInterfaces/    # only if synchronous service-to-service calls are required
```

Rules:

- Put only stable cross-service contracts here.
- Do not copy entity types into this area.

## Step 3. Create the service pair

Create:

```text
src/Services/{Subdomain}/
├── {Subdomain}Service.API/
│   ├── Program.cs
│   ├── HandlersCommand/
│   ├── HandlersQuery/
│   ├── HandlersEvent/
│   └── BackgroundWorkers/
├── {Subdomain}Service.Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── DomainServices/
│   ├── Events/
│   ├── Interfaces/
│   ├── Configurations/
│   ├── Utilities/
│   └── Repository/
```

Use `monica-project-unit-development` to fill the correct ProjectUnits inside those projects.

- Keep pure helpers in `{Subdomain}Service.Domain/Utilities/` and name them `Utils*`.
- Keep repository implementations, `DbContext`, and EF mapping in `{Subdomain}Service.Domain/Repository/`.
- Use the strict solution-project reference chain `{Subdomain}Service.API -> {Subdomain}Service.Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Put `[assembly: AutoControllerConfig(DefaultRoutePrefix = "api/v1", DomainName = "{Subdomain}")]` in `{Subdomain}Service.API/Program.cs` so handlers only declare request-level routes.

## Step 4. Add persistence ownership

- If the new service owns data, create or extend its migration project under `src/Migrations/{Subdomain}/`.
- Keep each service responsible for its own persistence model and migrations.

## Step 5. Wire the host and update the solution

- Register the service in the solution's host or gateway `Program.cs`.
- Keep the service's default `ApplicationService` route config in `{Subdomain}Service.API/Program.cs`, even if the solution also has a gateway or AppHost.
- If the host must consume `Configurations/*Options` during registration, register `Mo.AddConfiguration(o => o.AppConfiguration = builder.Configuration)` and then call `Mo.RegisterInstantly(builder)` before later registrations use those options.
- Keep orchestration metadata outside the domain projects.
- Keep `.slnx` folders aligned with `src/AppHost`, `src/Shared`, `src/Services`, and `src/Migrations`.

## Decision Rule

Create a new service only when the subdomain owns distinct business capability and data. Do not create a service just because a new HTTP endpoint appeared.
