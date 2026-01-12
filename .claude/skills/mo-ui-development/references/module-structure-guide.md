# MoFramework UI Module Structure Guide

This guide defines the standardized structure and patterns for creating UI modules in the MoFramework.

## Variable Naming Conventions

When creating UI modules, use these naming patterns:

| Variable | Format | Example |
|----------|--------|---------|
| `$ModuleName$` | PascalCase | `SignalR`, `SystemInfo` |
| `$ModuleUIName$` | `{ModuleName}UI` | `SignalrUI`, `SystemInfoUI` |
| `$UIFolderName$` | `UI{ModuleName}` | `UISignalr`, `UISystemInfo` |
| `$PageName$` | `UI{ModuleName}Page` | `UISignalRPage`, `UISystemInfoPage` |
| `$RouteURL$` | kebab-case | `/{module-name}-debug`, `/{module-name}-manage` |

## Module File Structure

### UI Module Class File

- **Location**: `MoLibrary.Framework.UI/Modules/{ModuleUIName}.cs`
- **Class naming**: `Module{ModuleUIName}`
- **Example**: `Modules/SignalrUI.cs` contains `ModuleSignalrUI`

### UI Folder Structure

```
UI{ModuleName}/
├── Components/     # Blazor components specific to this module
├── Models/         # Data models (only create when necessary - reuse source module models)
└── Services/       # Business services (if not using source module services)
```

### Page File

- **Location**: `MoLibrary.Framework.UI/Pages/{PageName}.razor`
- **File naming**: `UI{ModuleName}Page.razor`
- **Example**: `Pages/UISignalRPage.razor`

## Module Class Implementation

UI modules inherit from `MoModuleWithDependencies` and follow this pattern:

```csharp
public class Module{ModuleUIName}(Module{ModuleUIName}Option option)
    : MoModuleWithDependencies<Module{ModuleUIName}, Module{ModuleUIName}Option, Module{ModuleUIName}Guide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.{ModuleUIName};
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register module-specific services
        services.AddScoped<{ModuleName}Service>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.Disable{ModuleName}Page)
        {
            DependsOnModule<Module{ModuleName}Guide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<{PageName}>(
                    {PageName}.{ModuleName}_DEBUG_URL,
                    "{ModuleName} Debug",
                    Icons.Material.Filled.Settings,
                    "System Management",
                    addToNav: true,
                    navOrder: 100));
        }
    }
}
```

### Page Route Definition

```csharp
@attribute [Route({ModuleName}_DEBUG_URL)]

@code {
    public const string {ModuleName}_DEBUG_URL = "/{route-url}";
}
```

### Page Dependency Injection

```csharp
@using MoLibrary.Framework.UI.{UIFolderName}.Components
@using MoLibrary.Framework.UI.{UIFolderName}.Services
@using MoLibrary.Framework.UI.{UIFolderName}.Models
@inject {ModuleName}Service {ModuleName}Service
```

## Service Layer Development

### Service Implementation Pattern

Services implement business logic directly without going through HTTP API calls:

```csharp
/// <summary>
/// {ModuleName} service - implements core business logic
/// </summary>
public class {ModuleName}Service
{
    private readonly ILogger<{ModuleName}Service> _logger;
    private readonly IOtherService _otherService;

    public {ModuleName}Service(ILogger<{ModuleName}Service> logger, IOtherService otherService)
    {
        _logger = logger;
        _otherService = otherService;
    }

    /// <summary>
    /// Business method implementation
    /// </summary>
    /// <param name="parameter">Parameter</param>
    /// <returns>Never returns null - returns Res.Fail on error</returns>
    public async Task<Res<TResponse>> GetDataAsync(TRequest parameter)
    {
        try
        {
            var result = await DoBusinessLogic(parameter);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Business operation failed");
            return Res.Fail($"Operation failed: {ex.Message}");
        }
    }

    private async Task<TResponse> DoBusinessLogic(TRequest parameter)
    {
        // Concrete business logic implementation
        // ...
        return result;
    }
}
```

### Important Rules

1. **Service location**: Service classes should be defined in the **source module**, not the UI module. The UI module directly uses services from the source module.

2. **Return values**: All service methods must return `Res<T>` or `Res`. Never return null.

3. **Error handling**: Catch exceptions and return `Res.Fail` with meaningful error messages.

4. **Anonymous types**: If source module Minimal APIs return anonymous types, create proper DTO classes in the service layer.

5. **Required using**: Always include `using MoLibrary.Tool.MoResponse;` for Res types.

## Minimal API Refactoring

When a source module contains Minimal API definitions, refactor them to the service layer pattern.

### Refactoring Principles

- **Service in source module**: Service class must be defined in the source module, not the UI module
- **Business logic migration**: Move all business logic from Minimal API to Service class
- **Interface abstraction**: Define interface for the service to support DI and testing
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

### After: Refactored Minimal API

```csharp
// Minimal API - thin layer that delegates to service
endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish",
    async ([FromRoute] string eventKey,
          [FromServices] IDomainEventService domainEventService,
          [FromBody] JsonNode eventContent) =>
    {
        return await domainEventService.PublishDomainEventAsync(eventKey, eventContent);
    });
```

### After: Service Implementation (in source module)

```csharp
// Interface definition
public interface IDomainEventService
{
    Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent);
}

// Implementation (must be in source module)
public class DomainEventService(IMoDistributedEventBus eventBus, IGlobalJsonOption jsonOption)
    : IDomainEventService
{
    public async Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } unitEvent)
        {
            var json = eventContent.ToString();
            var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonOption.GlobalOptions)!;

            await eventBus.PublishAsync(unitEvent.Type, eventToPublish);

            return Res.Ok(eventToPublish)
                      .AppendMsg($"Published {eventKey} event");
        }
        return Res.Fail($"Failed to get {eventKey} unit information");
    }
}
```

## Data Model Management

### Model Organization

- **Location**: `UI{ModuleName}/Models/` directory
- **Use strongly-typed models**, avoid dynamic types
- **Naming should clearly express purpose**

### Model Management Principles

1. **Prioritize reusing source module models**: Use existing models from the source module whenever possible

2. **Composition over redefinition**: When new models are needed, compose them from source module models

3. **Minimize model creation**: Only create new data transfer objects when absolutely necessary

### Naming Conventions

- Request models: `{Feature}Request`
- Response models: `{Feature}Response`
- View models: `{Feature}ViewModel`

## Example References

### UISignalR Module

```
Modules/SignalrUI.cs                           # Module class
UISignalr/                                     # UI folder
├── Components/SignalRConnectionConfig.razor   # Components
├── Components/SignalRMessageLog.razor
└── Services/SignalRService.cs                 # Service (if UI-specific)
Pages/UISignalRPage.razor                      # Page
```

### UISystemInfo Module (with Service Layer)

```
Modules/SystemInfoUI.cs                        # Module class
UISystemInfo/                                  # UI folder
├── Services/SystemInfoService.cs              # Direct business logic
├── Controllers/ModuleSystemInfoController.cs  # Optional controller
└── Models/SystemInfoResponse.cs               # Response model
Pages/UISystemInfoPage.razor                   # Page
```
