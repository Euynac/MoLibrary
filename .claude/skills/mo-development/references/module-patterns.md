# Monica Module Patterns

This guide defines the standardized patterns and conventions for creating modules in Monica.

## Module Naming Conventions

| Component | Naming Pattern | Example |
|-----------|---------------|---------|
| Module class | `Module{Name}` | `ModuleSignalR`, `ModuleJobScheduler` |
| Options class | `Module{Name}Option` | `ModuleSignalROption` |
| Guide class | `Module{Name}Guide` | `ModuleSignalRGuide` |
| Builder extensions | `extension(Mo)` with `Add{Name}()` | `Mo.AddSignalR()` |
| Module key entry | `EMoModuleKey.{Name}` | `EMoModuleKey.SignalR` |

## File Structure

### Standard Infrastructure Module

```
Monica.{ModuleName}/
├── Modules/
│   └── Module{Name}.cs               # Core module implementation
├── Services/                         # Module services
│   ├── I{Feature}Service.cs          # Service interfaces
│   └── {Feature}Service.cs           # Service implementations
├── Models/                           # Data models (if needed)
└── Middleware/                       # Module middleware (if needed)
```

## Module Class Implementation

### Basic Module

```csharp
public class Module{Name}(Module{Name}Option option)
    : MoModule<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.{Name};

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<I{Name}Service, {Name}Service>();
    }
}
```

### Module That Declares Dependencies

Modules inherit from `MoModule<TModuleSelf, TModuleOption, TModuleGuide>` and declare required modules in `ClaimDependencies()`.

```csharp
public class Module{Name}(Module{Name}Option option)
    : MoModule<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.{Name};

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<I{Name}Service, {Name}Service>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleOtherGuide>().Register();
        DependsOnModule<ModuleAnotherGuide>().Register();
    }
}
```

## Options Class

Options classes inherit from `MoModuleOption<TModule>` or `MoModuleOptionWithMinimalApi<TModule>` (for modules that expose Minimal API endpoints):

```csharp
// Standard module options
public class Module{Name}Option : MoModuleOption<Module{Name}>
{
    public bool EnableFeature { get; set; } = true;
    public int MaxItems { get; set; } = 100;
    public string ConnectionString { get; set; } = string.Empty;
}

// Module with Minimal API endpoints
public class Module{Name}Option : MoModuleOptionWithMinimalApi<Module{Name}>
{
    public bool EnableFeature { get; set; } = true;
}
```

## Guide Class (Fluent Configuration)

```csharp
public class Module{Name}Guide : MoModuleGuide<Module{Name}, Module{Name}Option, Module{Name}Guide>
{
    public Module{Name}Guide EnableFeature(bool enable = true)
    {
        Option.EnableFeature = enable;
        return this;
    }

    public Module{Name}Guide WithMaxItems(int maxItems)
    {
        Option.MaxItems = maxItems;
        return this;
    }
}
```

## Builder Extensions

Builder extensions use the `extension(Mo)` syntax:

```csharp
public static Module{Name}Guide Add{Name}(Action<Module{Name}Option>? action = null)
{
    // Registration logic handled by Mo infrastructure
}
```

Usage:

```csharp
// With options action
Mo.Add{Name}(options =>
{
    options.EnableFeature = true;
    options.MaxItems = 50;
});

// With fluent guide
Mo.Add{Name}()
    .EnableFeature()
    .WithMaxItems(50);

// Dependencies are automatically registered
Mo.AddJobSchedulerUI(options =>
{
    options.DisableJobSchedulerPage = false;
});
// ModuleJobScheduler and ModuleUICore are automatically added
```

## UI Modules

UI module architecture patterns (Mixed, Standalone, Framework), UI module class implementation, and folder conventions are documented in the **mo-ui-development** skill's `references/module-structure-guide.md`.

## Service Layer Integration

### UI Service Pattern (Res<T>)

UI services — those directly consumed by Blazor components — use `Res<T>` / `Res` return types:

```csharp
public class {Name}UIService(
    ILogger<{Name}UIService> logger,
    IOtherDependency dependency)
{
    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        try
        {
            var result = await dependency.ProcessAsync(request);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operation failed");
            return Res.Fail($"Operation failed: {ex.Message}");
        }
    }

    public async Task<Res> ExecuteActionAsync(TRequest request)
    {
        // Implementation
        return Res.Ok();
    }
}
```

### Infrastructure Service Pattern (Exceptions)

Non-UI / infrastructure services use standard .NET patterns — direct return types and throw exceptions:

```csharp
public class {Name}Service(
    ILogger<{Name}Service> logger,
    IOtherDependency dependency)
{
    public async Task<TResponse> GetDataAsync(TRequest request)
    {
        var result = await dependency.ProcessAsync(request);
        return result ?? throw new KeyNotFoundException("Data not found");
    }

    public async Task ExecuteActionAsync(TRequest request)
    {
        if (!IsValid(request))
            throw new InvalidOperationException("Invalid request");

        await dependency.ProcessAsync(request);
    }
}
```

### Using Options in Services

```csharp
public class {Name}UIService(
    IOptions<Module{Name}Option> options,
    ILogger<{Name}UIService> logger)
{
    private readonly Module{Name}Option _options = options.Value;

    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        if (!_options.EnableFeature)
        {
            return "Feature is disabled";
        }

        // Use _options.MaxItems, etc.
    }
}
```

## Minimal API to Service Layer Refactoring

When a source module contains Minimal API definitions with inline business logic, refactor them to the service layer pattern.

### Principles

- **Service in source module**: Service class must be defined in the source module, not the UI module
- **Business logic migration**: Move all business logic from Minimal API to service class
- **Interface abstraction**: Define interface for the service to support DI
- **Model reuse**: Reuse existing models from the source module instead of creating duplicates

### Before: Minimal API with Inline Logic

```csharp
endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish",
    async ([FromRoute] string eventKey,
          [FromServices] IMoDistributedEventBus eventBus,
          [FromServices] IGlobalJsonOption jsonOption,
          [FromBody] JsonNode eventContent,
          HttpResponse response,
          HttpContext context) =>
{
    if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } e)
    {
        var json = eventContent.ToString();
        var eventToPublish = JsonSerializer.Deserialize(json, e.Type, jsonOption.GlobalOptions)!;
        await eventBus.PublishAsync(e.Type, eventToPublish);
        return Res.Ok(eventToPublish).AppendMsg($"Published {eventKey} event").GetResponse();
    }

    return Res.Fail($"Failed to get {eventKey} unit information").GetResponse();
});
```

### After: Thin Minimal API + Service

```csharp
// Minimal API - thin delegation layer
endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish",
    async ([FromRoute] string eventKey,
          [FromServices] IDomainEventService domainEventService,
          [FromBody] JsonNode eventContent) =>
    {
        return await domainEventService.PublishDomainEventAsync(eventKey, eventContent);
    });
```

```csharp
// Service implementation (in source module)
public class DomainEventService(
    IMoDistributedEventBus eventBus,
    IGlobalJsonOption jsonOption) : IDomainEventService
{
    public async Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } unitEvent)
        {
            var json = eventContent.ToString();
            var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonOption.GlobalOptions)!;
            await eventBus.PublishAsync(unitEvent.Type, eventToPublish);
            return Res.Ok(eventToPublish).AppendMsg($"Published {eventKey} event");
        }
        return Res.Fail($"Failed to get {eventKey} unit information");
    }
}
```

## Best Practices

1. **Use primary constructors** for dependency injection
2. **Keep modules focused** — one module, one responsibility
3. **Declare dependencies explicitly** in `ClaimDependencies()` using `DependsOnModule<TGuide>().Register()`
4. **Use options for configuration** — inject `IOptions<TOption>`
5. **Follow naming conventions** — consistent naming makes code discoverable
6. **Return Res types only in UI services** — infrastructure services use standard returns + exceptions
7. **UI module patterns** — see mo-ui-development skill for UI module architecture and folder conventions
