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

## Error Handling and Build Safety

The AutoController generator includes comprehensive error detection that will cause builds to fail with detailed messages when issues are found:

### Common Error Scenarios

```csharp
// ❌ AC0001: Missing configuration when using default routing
// No [AutoControllerGeneratorConfig] attribute and RequireExplicitRoutes = false

// ❌ AC0002: Invalid route prefix format
[assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "/api/v1/")]  // Leading/trailing slashes

// ❌ AC0003: Invalid inheritance
public class BadHandler : ApplicationService  // Missing generic arguments
{
    // ...
}

// ❌ AC0007: Route conflicts
public class GetUserHandler : ApplicationService<GetUserQuery, GetUserResponse>
{
    [HttpGet("user")]  // Same route as another handler
    public async Task<GetUserResponse> Handle(GetUserQuery request) { ... }
}

public class FindUserHandler : ApplicationService<FindUserQuery, FindUserResponse>
{
    [HttpGet("user")]  // ❌ Conflict with GetUserHandler
    public async Task<FindUserResponse> Handle(FindUserQuery request) { ... }
}
```

### Build Output Examples

When errors occur, you'll see detailed build failures:

```console
error AC0001: Missing [AutoControllerGeneratorConfig] attribute when default routing is required
  Add [assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "api/v1")] to Program.cs

error AC0003: Class 'BadHandler' does not properly inherit from ApplicationService<TRequest, TResponse>
  Ensure the base class has exactly 2 generic type arguments

error AC0007: Route conflict detected. Multiple handlers use route 'api/v1/user' with GET method: GetUser, FindUser
  Ensure each handler has a unique route or HTTP method combination
```

### Error Prevention Tips

1. **Always configure properly**: Include the assembly-level attribute with valid route prefixes
2. **Use unique routes**: Ensure each handler has a distinct route/HTTP method combination
3. **Follow naming conventions**: Use `Query*Handler` or `Command*Handler` for CQRS auto-detection
4. **Validate inheritance**: Ensure proper `ApplicationService<TRequest, TResponse>` inheritance
5. **Check method signatures**: Use `Task<TResponse> Handle(TRequest request)` pattern

## Project References

Make sure your target project references:

```xml
<ItemGroup>
  <PackageReference Include="MoLibrary.DomainDrivenDesign" Version="..." />
  <Analyzer Include="MoLibrary.Generators.AutoController" Version="..." />
</ItemGroup>
```

## Troubleshooting

If builds fail with AutoController errors:

1. **Check configuration**: Verify your `[AutoControllerGeneratorConfig]` attribute syntax
2. **Review class structure**: Ensure proper inheritance from `ApplicationService<TRequest, TResponse>`
3. **Validate routes**: Look for duplicate route/HTTP method combinations
4. **Check naming**: Follow CQRS conventions for automatic HTTP method detection
5. **Rebuild solution**: Source generators may require a clean rebuild to detect changes