# Unified Result Model Res - Complete Guide

This guide documents Monica's lightweight result-envelope model built around `Res` and `Res<T>`.

**Source location**: `Monica.Tool/Results/Res.cs`

**Related types**: `IResultEnvelope`, `ResStatus`, `ResPaged<T>`, `ResExtensions`

## Scope

**`Res`/`Res<T>` is only for UI module-related services** — services directly consumed by Blazor components or UI layers where the `IsFailed` pattern is used for error handling in the view.

**Non-UI / infrastructure modules** must use standard .NET patterns: direct return types and throw exceptions (e.g., `KeyNotFoundException`, `FileNotFoundException`, `InvalidOperationException`) for error cases. Do not use `Res` in these modules.

## Overview

The `Res` and `Res<T>` types provide a consistent way to return result envelopes from service methods, supporting:
- Success/failure status
- Error messages and statuses
- Data payload (for `Res<T>`)
- Optional metadata in `Metadata`
- Implicit conversions for cleaner code

The CLR model and the serialized contract now align. With the repository's camelCase JSON policy, envelopes are serialized as `message`, `status`, `metadata`, and `data`.

## Res<T> Generic Type

### Implicit Conversions

The `Res<T>` type supports powerful implicit conversions that make code more readable:

#### When method returns `Res<T>`:

```csharp
public async Task<Res<UserData>> GetUserAsync(int id)
{
    var user = await repo.GetUserById(id);

    // Return error - string converts to Res<T> with Status 400
    if (user == null)
    {
        return "User not found";  // string => Res<T>, Data = null, Status = 400
    }

    // Equivalent explicit form:
    // return Res.Fail("User not found");

    // Return success - T instance converts to Res<T> with Status 200
    return user;  // T => Res<T>, Status = 200
}
```

#### When method returns `Res` (no data):

```csharp
public async Task<Res> DeleteUserAsync(int id)
{
    if (!(await repo.Exists(id)))
    {
        return "User does not exist";  // string => Res, Status = 400
        // Equivalent: return Res.Fail("User does not exist");
    }

    await repo.Delete(id);
    return Res.Ok();  // Success with no data
}
```

### Special Case: Res<string>

When the return type is `Res<string>`, you **CANNOT** use implicit string conversion for success (it would be ambiguous). Use explicit methods:

```csharp
public async Task<Res<string>> GetUserNameAsync(int id)
{
    var user = await repo.GetUserById(id);

    // Return error - must use explicit Fail
    if (user == null)
    {
        return Res.Fail("User not found");
    }

    // Return success - must use explicit Ok<string>
    return Res.Ok<string>(user.Name);
}
```

## Best Practices

### Async Methods with Res<T>

Use implicit conversions for clean, readable code:

```csharp
public override async Task<Res<ResponseUserCheck>> CheckUser(
    QueryUserCheck request,
    CancellationToken cancellationToken)
{
    var userInfo = await repo.GetUserInfo(request.Username);

    if (userInfo == null)
    {
        return $"Username {request.Username} does not exist";
    }

    return _mapper.Map<ResponseUserCheck>(userInfo);
}
```

### Async Methods with Res

```csharp
public override async Task<Res> ValidateUser(
    User user,
    CancellationToken cancellationToken)
{
    if (!(await repo.Exists(user.Id)))
    {
        return "User does not exist";
    }

    if (!user.IsValid())
    {
        return "User data is invalid";
    }

    return Res.Ok();
}
```

## Response Handling Patterns

### Handling Res<T> Responses

Use the `IsFailed` pattern to extract both error and data in one operation:

```csharp
// Pattern: No need to define a new result variable
if ((await userManager.CheckUser(req)).IsFailed(out var error, out var data))
{
    // Handle error
    return error;
}

// At this point, 'data' contains the ResponseUserCheck
ProcessUser(data);
```

### Handling Res Responses (No Data)

```csharp
if ((await userManager.ValidateUser(user)).IsFailed(out var error))
{
    return error;
}

// Continue with validated user
```

### Chaining Multiple Operations

```csharp
public async Task<Res<Order>> ProcessOrderAsync(OrderRequest request)
{
    // Validate user
    if ((await _userService.ValidateUser(request.UserId)).IsFailed(out var userError))
    {
        return userError.Message;  // Convert error message to new Res<Order> error
    }

    // Get product
    if ((await _productService.GetProduct(request.ProductId)).IsFailed(out var productError, out var product))
    {
        return productError.Message;
    }

    // Create order
    var order = new Order { Product = product, UserId = request.UserId };
    return order;
}
```

## Service Layer Implementation

### Standard Service Pattern

```csharp
public class UserUIService(
    ILogger<UserUIService> logger,
    IUserRepository repository)
{
    public async Task<Res<UserResponse>> GetUserAsync(int id)
    {
        try
        {
            var user = await repository.GetByIdAsync(id);

            if (user == null)
            {
                return "User not found";
            }

            return new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user {UserId}", id);
            return Res.Fail($"Failed to get user: {ex.Message}");
        }
    }
}
```

### Important Rules

1. **Only use in UI-facing services** — infrastructure modules use standard returns + exceptions
2. **Never return null** - Always return `Res.Fail()` or `Res.Ok()`
3. **Catch exceptions** - Return `Res.Fail()` with meaningful error messages
4. **Use implicit conversions** - Makes code cleaner and more readable
5. **Include using statement** - `using Monica.Tool.Results;`
6. **Attach structured error payloads** - use `AppendMetadata("error", payload)` when extra error detail is needed

## API Response Integration

When using `Res` with Minimal APIs or Controllers:

```csharp
// Minimal API
endpoints.MapGet("/users/{id}", async (int id, IUserService userService) =>
{
    var result = await userService.GetUserAsync(id);
    return result.GetResponse();  // Converts to appropriate HTTP response
});

// With message appending
return Res.Ok(data).AppendMessage("Operation completed successfully").GetResponse();
```

Use `GetResponse()` when you want Monica's standard wire contract (`message`, `status`, `data`, `metadata`).

For external APIs that should expose a custom response shape without changing `Res` itself, implement a projector and opt into `GetProjectedResponse(...)`:

```csharp
public sealed record PublicApiRes(string Msg, ResStatus Status, object? Payload, object? Extra);

public sealed class PublicApiResProjector : IResultProjector<PublicApiRes>
{
    public PublicApiRes Project(IResultEnvelope response)
    {
        var payload = response.GetType().GetProperty(nameof(Res<object>.Data))?.GetValue(response);

        return new PublicApiRes(
            response.Message ?? string.Empty,
            response.Status,
            payload,
            response.Metadata);
    }
}

Mo.Options.ResultProjector = new PublicApiResProjector();

// Controller
return result.GetProjectedResponse(this);

// Minimal API
return result.GetProjectedResponse(httpContext);
```

## Summary Table

| Scenario | Code Pattern |
|----------|-------------|
| Return success with data | `return data;` or `return Res.Ok(data);` |
| Return error | `return "error message";` or `return Res.Fail("error message");` |
| Return success (no data) | `return Res.Ok();` |
| Return `Res<string>` success | `return Res.Ok<string>("value");` |
| Check for failure | `if (result.IsFailed(out var error, out var data))` |
| Check for failure (no data) | `if (result.IsFailed(out var error))` |
| Propagate error | `return error;` or `return error.Message;` |
