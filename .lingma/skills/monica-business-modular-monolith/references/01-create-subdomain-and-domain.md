# Create a New Subdomain Domain Package

Use this workflow when a new bounded context should live inside the same deployable application.

## Step 1. Name the bounded context

- Choose the business name first, such as `Ordering`, `Billing`, or `Warehouse`.
- Use that name consistently for the shared language area and the merged domain package:
  - `Domain{Subdomain}` in `Platform.Protocol/PublishedLanguages`
  - `Domains.{Subdomain}.csproj`

- If the solution does not already have a suitable entry project, create one with an explicit solution name, preferably ending with `Api`, such as `Monica.Docs.Api`.

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
├── Domains.{Subdomain}.csproj
├── AutoControllerGeneratorConfig.cs
├── Application/
│   ├── HandlersCommand/
│   ├── HandlersQuery/
│   ├── HandlersEvent/
│   └── BackgroundWorkers/
├── Interfaces/
├── DomainServices/
├── Configurations/
├── Utilities/
└── Repository/
```

Use `monica-project-unit-development` to place domain-owned units inside this project:

- `ApplicationService` units go under `Application/HandlersCommand/` or `Application/HandlersQuery/`.
- Event handlers go under `Application/HandlersEvent/`.
- Jobs go under `Application/BackgroundWorkers/`.
- `DomainService` units go under `DomainServices/`.
- Pure helpers go under `Utilities/` and use `Utils*` names.
- Keep repository implementations, `DbContext`, and EF mapping in `Repository/`.
- Add one assembly-level `AutoControllerConfig(DefaultRoutePrefix = "api/v1", DomainName = "{Subdomain}")` file at the project root so handlers do not repeat the same class-level base route.

## Step 4. Keep AppHost as the Program-only entry

Create or extend an AppHost entry project:

```text
src/AppHost/{ProjectName}/
├── {ProjectName}.csproj
└── Program.cs
```

Keep AppHost as the composition root only. Do not place handlers, jobs, repositories, utilities, or other business ProjectUnits here.

## Step 5. Register the domain and update the solution

- Add domain registration and host composition in `Program.cs`.
- Keep the default `ApplicationService` base route inside the domain project root config file, not in AppHost `Program.cs`.
- If AppHost registration must consume `Configurations/*Options`, register `Mo.AddConfiguration(o => o.AppConfiguration = builder.Configuration)` and then call `Mo.RegisterInstantly(builder)` before later registrations use those options.
- Keep `.slnx` folders aligned with `src/AppHost`, `src/Shared`, and `src/Domains`.

## Step 6. Add persistence ownership

- Add or extend the domain's `Repository` area and the shared migrator entry point.
- Keep ownership explicit even though deployment is shared.

## Decision Rule

Create a new domain package only when the subdomain has distinct language, invariants, and ownership. Do not create one for every page or CRUD screen.
