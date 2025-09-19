# AutoController Source Generator Configuration

## Overview

The AutoController source generator supports configuration through assembly-level attributes, allowing you to set default routing behavior when developers don't add explicit `[Route]` attributes to ApplicationService classes.

## Configuration Options

### Assembly-Level Configuration

Add the following to your `Program.cs` or `AssemblyInfo.cs`:

```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

// Example 1: Simple route prefix only
[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1"
)]

// Example 2: Route prefix with domain name
[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "Flight"
)]

// Example 3: Strict mode requiring explicit routes
[assembly: AutoControllerGeneratorConfig(
    RequireExplicitRoutes = true
)]
```

### Configuration Properties

- **`DefaultRoutePrefix`**: The default route prefix to use (e.g., "api/v1")
- **`DomainName`**: Optional domain name to include in routes (e.g., "Flight", "User", "Order")
- **`RequireExplicitRoutes`**: When `true`, requires explicit `[Route]` attributes (default: `false`)

### Route Generation Pattern

The generator follows this pattern:
- **With DomainName**: `{DefaultRoutePrefix}/{DomainName}`
- **Without DomainName**: `{DefaultRoutePrefix}`

## Usage Examples

### Scenario 1: Route Prefix Only

With configuration:
```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

[assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "api/v1")]
```

Your ApplicationService:
```csharp
// No [Route] attribute needed!
public class GetFlightQueryHandler : ApplicationService<GetFlightQuery, GetFlightResponse>
{
    [HttpGet]
    public async Task<GetFlightResponse> Handle(GetFlightQuery request)
    {
        // Implementation
    }
}
```

Generated route: `api/v1`

### Scenario 2: Route with Domain Name

With configuration:
```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "Flight"
)]
```

Generated route: `api/v1/Flight`

### Scenario 3: Explicit Routes Override

Even with default configuration, explicit routes take precedence:
```csharp
[Route("custom/flight/search")]  // This overrides defaults
public class GetFlightQueryHandler : ApplicationService<GetFlightQuery, GetFlightResponse>
{
    // ...
}
```

### Scenario 4: Mixed Approach

```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "Flight",
    RequireExplicitRoutes = false
)]

// Uses default: api/v1/Flight
public class GetFlightQueryHandler : ApplicationService<...> { }

// Uses explicit route
[Route("api/v2/flights/advanced-search")]
public class AdvancedFlightSearchQueryHandler : ApplicationService<...> { }
```

## Route Generation Logic

1. **Explicit Route First**: If a `[Route]` attribute exists, it's used
2. **Configuration Fallback**: If no explicit route and `RequireExplicitRoutes = false`:
   - **With DomainName**: `{DefaultRoutePrefix}/{DomainName}`
   - **Without DomainName**: `{DefaultRoutePrefix}`

## Migration Guide

### Existing Projects
- **Backward Compatible**: All existing `[Route]` attributes continue to work
- **Gradual Migration**: Remove `[Route]` attributes as needed, let defaults take over

### Best Practices
- Use `DefaultRoutePrefix` for consistent API versioning
- Use `DomainName` to group related controllers under a domain
- Set `RequireExplicitRoutes = true` for strict control
- Document your routing conventions in team guidelines

## CQRS Method Routing Convention

The generator now supports CQRS-based method routing with automatic HTTP method detection:

### Basic Pattern
- **Controller Route**: `{DefaultRoutePrefix}/{DomainName}` (if DomainName exists) or `{DefaultRoutePrefix}`
- **Method Route**: `{method-name}` (kebab-case) placed on individual methods
- **Method Name Conversion**: Handler class names are converted to kebab-case
  - `QueryGetUserName` → `get-user-name`
  - `CommandCreateUser` → `create-user`
- **HTTP Method Detection**:
  - Query handlers default to `HttpGet`
  - Command handlers default to `HttpPost`

### Example

```csharp
using MoLibrary.DomainDrivenDesign.AutoController.Attributes;

[assembly: AutoControllerGeneratorConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "User"
)]

// No HTTP method attribute needed!
public class QueryGetUserProfileHandler : ApplicationService<QueryGetUserProfile, GetUserProfileResponse>
{
    public async Task<GetUserProfileResponse> Handle(QueryGetUserProfile request)
    {
        // Implementation
        return new GetUserProfileResponse();
    }
}

// Generated controller: [Route("api/v1/User")]
// Generated method: [HttpGet("get-user-profile")]
// Final URL: GET api/v1/User/get-user-profile
```

### Override Behavior

You can still override the defaults:

```csharp
public class QueryGetUserProfileHandler : ApplicationService<QueryGetUserProfile, GetUserProfileResponse>
{
    [HttpPost("custom-endpoint")]  // Overrides default GET with custom route
    public async Task<GetUserProfileResponse> Handle(QueryGetUserProfile request)
    {
        // Implementation
    }
}

// Alternative: Override just the HTTP method, keep auto-generated route
public class QueryGetUserProfileHandler : ApplicationService<QueryGetUserProfile, GetUserProfileResponse>
{
    [HttpPost]  // Empty template uses auto-generated method route
    public async Task<GetUserProfileResponse> Handle(QueryGetUserProfile request)
    {
        // Implementation
    }
}

// Generated controller: [Route("api/v1/User")]
// Generated method: [HttpPost("get-user-profile")]
// Final URL: POST api/v1/User/get-user-profile
```

## Error Handling and Build Failures

The AutoController source generator includes comprehensive error handling that will cause builds to fail with detailed error messages when issues are detected:

### Error Codes and Messages

| Error Code | Description | Example Message |
|------------|-------------|-----------------|
| AC0001 | Missing configuration | Missing [AutoControllerGeneratorConfig] attribute when default routing is required |
| AC0002 | Invalid route prefix | DefaultRoutePrefix '/api/v1/' is invalid. Remove leading/trailing slashes |
| AC0003 | Invalid inheritance | Class 'MyHandler' does not properly inherit from ApplicationService<TRequest, TResponse> |
| AC0004 | Missing Handle method | Class 'MyHandler' does not contain a public Handle method |
| AC0005 | Invalid method signature | Handle method has invalid signature. Expected: Task<TResponse> Handle(TRequest request) |
| AC0006 | Missing HTTP attribute | Method requires HTTP method attribute or CQRS naming convention |
| AC0007 | Route conflict | Multiple handlers use route 'api/v1/user' with GET method |
| AC0008 | Configuration failed | Failed to extract configuration from assembly attributes |
| AC0009 | Handler extraction failed | Failed to extract handler information from class |
| AC0010 | Code generation failed | Failed to generate controller code |
| AC0011 | Invalid route template | Route template contains invalid characters or format |
| AC0012 | Unknown handler type | Cannot determine if handler is Command or Query type |

### Build Failure Examples

When errors occur, the build will fail with clear messages:

```console
error AC0001: Missing [AutoControllerGeneratorConfig] attribute when default routing is required
  Add [assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "api/v1")] to Program.cs

error AC0003: Class 'GetUserHandler' does not properly inherit from ApplicationService<TRequest, TResponse>
  Ensure the base class has exactly 2 generic type arguments

error AC0007: Route conflict detected. Multiple handlers use route 'api/v1/user' with GET method: GetUser, FindUser
  Ensure each handler has a unique route or HTTP method combination
```

### Configuration Validation

The generator validates configuration at build time:

```csharp
// ❌ This will cause build failure (leading slash)
[assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "/api/v1")]

// ✅ Correct format
[assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = "api/v1")]
```

### Handler Validation

Handler classes are validated for proper structure:

```csharp
// ❌ Missing generic arguments - will cause AC0003 error
public class BadHandler : ApplicationService
{
    // ...
}

// ✅ Correct inheritance
public class GoodHandler : ApplicationService<GetUserQuery, GetUserResponse>
{
    public async Task<GetUserResponse> Handle(GetUserQuery request)
    {
        // ...
    }
}
```

## Benefits

- **Reduced Boilerplate**: Less repetitive `[Route]` and `[Http*]` attributes
- **Consistency**: Enforced routing patterns across services
- **Domain Organization**: Clear separation by domain when using `DomainName`
- **CQRS Convention**: Automatic HTTP method selection based on handler type
- **Maintainability**: Centralized route configuration
- **Flexibility**: Mix explicit and default routing as needed
- **Compile-Time**: All configuration happens at build time
- **Build Safety**: Comprehensive error detection prevents runtime issues
- **Developer Experience**: Clear error messages with actionable guidance