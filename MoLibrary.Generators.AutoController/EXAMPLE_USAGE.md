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

With configuration `DefaultRoutePrefix = "api/v1", DomainName = "Flight"`:

- `GetFlightQueryHandler` → Route: `api/v1/Flight`
- `SearchFlightQueryHandler` → Route: `custom/flight/search` (explicit override)

With configuration `DefaultRoutePrefix = "api/v1"` (no DomainName):

- `GetFlightQueryHandler` → Route: `api/v1`
- `SearchFlightQueryHandler` → Route: `custom/flight/search` (explicit override)

## Project References

Make sure your target project references:

```xml
<ItemGroup>
  <PackageReference Include="MoLibrary.DomainDrivenDesign" Version="..." />
  <Analyzer Include="MoLibrary.Generators.AutoController" Version="..." />
</ItemGroup>
```