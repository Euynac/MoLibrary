# Example Usage

## Configuration Setup

In your project that references both `MoLibrary.DomainDrivenDesign` and `MoLibrary.Generators.AutoController`, add this configuration:

### Program.cs or AssemblyInfo.cs
```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

// Example 1: Simple route prefix only
[assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "api/v1")]

// Example 2: Route prefix with domain name
[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "Flight"
)]
```

## ApplicationService Implementation

```csharp
// Without explicit [Route] - uses configuration defaults
public class GetFlightQueryHandler : ApplicationService<GetFlightQuery, GetFlightResponse>
{
    [HttpGet]
    public async Task<GetFlightResponse> Handle(GetFlightQuery request)
    {
        // Implementation
        return new GetFlightResponse();
    }
}

// With explicit [Route] - overrides defaults
[Route("custom/flight/search")]
public class SearchFlightQueryHandler : ApplicationService<SearchFlightQuery, SearchFlightResponse>
{
    [HttpGet]
    public async Task<SearchFlightResponse> Handle(SearchFlightQuery request)
    {
        // Implementation
        return new SearchFlightResponse();
    }
}
```

## Generated Routes

### CQRS Convention (New)

With configuration `DefaultRoutePrefix = "api/v1", DomainName = "Flight"`:

```csharp
// No HTTP attributes needed! Follows CQRS convention
public class QueryGetFlightHandler : ApplicationService<QueryGetFlight, GetFlightResponse>
{
    public async Task<GetFlightResponse> Handle(QueryGetFlight request)
    {
        // Implementation
        return new GetFlightResponse();
    }
}

public class CommandCreateFlightHandler : ApplicationService<CommandCreateFlight, CreateFlightResponse>
{
    public async Task<CreateFlightResponse> Handle(CommandCreateFlight request)
    {
        // Implementation
        return new CreateFlightResponse();
    }
}
```

Generated code:
- `QueryGetFlightHandler` → Controller: `[Route("api/v1/Flight")]`, Method: `[HttpGet("get-flight")]`
- `CommandCreateFlightHandler` → Controller: `[Route("api/v1/Flight")]`, Method: `[HttpPost("create-flight")]`

Final URLs:
- `GET api/v1/Flight/get-flight`
- `POST api/v1/Flight/create-flight`

### Classic Convention (Still Supported)

With explicit HTTP attributes:

```csharp
public class GetFlightQueryHandler : ApplicationService<GetFlightQuery, GetFlightResponse>
{
    [HttpGet]
    public async Task<GetFlightResponse> Handle(GetFlightQuery request)
    {
        // Implementation
        return new GetFlightResponse();
    }
}

[Route("custom/flight/search")]
public class SearchFlightQueryHandler : ApplicationService<SearchFlightQuery, SearchFlightResponse>
{
    [HttpGet]
    public async Task<SearchFlightResponse> Handle(SearchFlightQuery request)
    {
        // Implementation
        return new SearchFlightResponse();
    }
}
```

Generated code:
- `GetFlightQueryHandler` → Controller: `[Route("api/v1/Flight")]`, Method: `[HttpGet]` (no method route)
- `SearchFlightQueryHandler` → Controller: `[Route("custom/flight/search")]`, Method: `[HttpGet]`

Final URLs:
- `GET api/v1/Flight` (method uses controller route directly)
- `GET custom/flight/search`

## Project References

Make sure your target project references:

```xml
<ItemGroup>
  <PackageReference Include="MoLibrary.DomainDrivenDesign" Version="..." />
  <Analyzer Include="MoLibrary.Generators.AutoController" Version="..." />
</ItemGroup>
```