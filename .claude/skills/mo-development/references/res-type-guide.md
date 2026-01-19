# Unified Response Model Res - Complete Guide

This guide provides comprehensive documentation for the unified interface return model `Res` used throughout MoLibrary.

**Source location**: `MoLibrary.Tool/MoResponse/Res.cs`

## Overview

The `Res` and `Res<T>` types provide a consistent way to return results from service methods, supporting:
- Success/failure status
- Error messages and codes
- Data payload (for `Res<T>`)
- Implicit conversions for cleaner code

## Res<T> Generic Type

### Implicit Conversions

The `Res<T>` type supports powerful implicit conversions that make code more readable:

#### When method returns `Res<T>`:

```csharp
public async Task<Res<UserData>> GetUserAsync(int id)
{
    var user = await repo.GetUserById(id);

    // Return error - string converts to Res<T> with Code 400
    if (user == null)
    {
        return "User not found";  // string => Res<T>, Data = null, Code = 400
    }

    // Equivalent explicit form:
    // return Res.Fail("User not found");

    // Return success - T instance converts to Res<T> with Code 200
    return user;  // T => Res<T>, Code = 200
}
```

#### When method returns `Res` (no data):

```csharp
public async Task<Res> DeleteUserAsync(int id)
{
    if (!(await repo.Exists(id)))
    {
        return "User does not exist";  // string => Res, Code = 400
        // Equivalent: return Res.Fail("User does not exist");
    }

    await repo.Delete(id);
    return Res.Ok();  // Success with no data
}
```

### Special Case: Res<string>

When the return type is `Res<string>`, you **cannot** use implicit string conversion for success (it would be ambiguous). Use explicit methods:

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
public class UserService
{
    private readonly ILogger<UserService> _logger;
    private readonly IUserRepository _repository;

    public UserService(ILogger<UserService> logger, IUserRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<Res<UserResponse>> GetUserAsync(int id)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);

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
            _logger.LogError(ex, "Failed to get user {UserId}", id);
            return Res.Fail($"Failed to get user: {ex.Message}");
        }
    }
}
```

### Important Rules

1. **Never return null** - Always return `Res.Fail()` or `Res.Ok()`
2. **Catch exceptions** - Return `Res.Fail()` with meaningful error messages
3. **Use implicit conversions** - Makes code cleaner and more readable
4. **Include using statement** - `using MoLibrary.Tool.MoResponse;`

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
return Res.Ok(data).AppendMsg("Operation completed successfully").GetResponse();
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
