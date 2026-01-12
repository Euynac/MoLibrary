# MoLibrary Module Patterns

This guide defines the standardized patterns and conventions for creating modules in MoLibrary.

## Module Naming Conventions

| Component | Naming Pattern | Example |
|-----------|---------------|---------|
| Module class | `Module{Name}` | `ModuleSignalR`, `ModuleJobScheduler` |
| Options class | `Module{Name}Option` | `ModuleSignalROption` |
| Guide class | `Module{Name}Guide` | `ModuleSignalRGuide` |
| Builder extensions | `Module{Name}BuilderExtensions` | `ModuleSignalRBuilderExtensions` |
| Enum entry | `EMoModules.{Name}` | `EMoModules.SignalR` |

## File Structure

### Standard Module Structure

```
MoLibrary.{ModuleName}/
├── Module{Name}.cs                    # Core module implementation
├── Module{Name}Option.cs              # Configuration options
├── Module{Name}Guide.cs               # Fluent configuration builder
├── Module{Name}BuilderExtensions.cs   # WebApplicationBuilder extensions
├── Services/                          # Module services
│   ├── I{Feature}Service.cs           # Service interfaces
│   └── {Feature}Service.cs            # Service implementations
├── Models/                            # Data models (if needed)
└── Middleware/                        # Module middleware (if needed)
```

### UI Module Structure

```
MoLibrary.Framework.UI/
├── Modules/
│   └── {ModuleUI}UI.cs                # UI module class (e.g., SignalrUI.cs)
├── UI{ModuleName}/                    # UI folder (e.g., UISignalr/)
│   ├── Components/                    # Blazor components
│   ├── Models/                        # View models (if needed)
│   └── Services/                      # UI-specific services (if needed)
└── Pages/
    └── UI{ModuleName}Page.razor       # Page component
```

## Module Class Implementation

### Basic Module

```csharp
public class Module{Name}(Module{Name}Option option)
    : MoModule<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.{Name};
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register module services
        services.AddScoped<I{Name}Service, {Name}Service>();
    }
}
```

### Module with Dependencies

```csharp
public class Module{Name}(Module{Name}Option option)
    : MoModuleWithDependencies<Module{Name}, Module{Name}Option, Module{Name}Guide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.{Name};
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<I{Name}Service, {Name}Service>();
    }

    public override void ClaimDependencies()
    {
        // Declare dependencies on other modules
        DependsOnModule<ModuleOtherGuide>().Register();
        DependsOnModule<ModuleAnotherGuide>().Register();
    }
}
```

### UI Module Implementation

For UI module implementation patterns including page registration and component structure, see the **MoLibrary UI Development** skill and its `references/module-structure-guide.md`.

## Options Class

```csharp
public class Module{Name}Option
{
    /// <summary>
    /// Enable or disable the feature
    /// </summary>
    public bool EnableFeature { get; set; } = true;

    /// <summary>
    /// Configuration value with default
    /// </summary>
    public int MaxItems { get; set; } = 100;

    /// <summary>
    /// Connection string or other sensitive config
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
```

## Guide Class (Fluent Configuration)

```csharp
public class Module{Name}Guide : MoModuleGuideBase<Module{Name}, Module{Name}Option>
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

    public Module{Name}Guide WithConnectionString(string connectionString)
    {
        Option.ConnectionString = connectionString;
        return this;
    }
}
```

## Builder Extensions

```csharp
public static class Module{Name}BuilderExtensions
{
    /// <summary>
    /// Configure {Name} module with options action
    /// </summary>
    public static WebApplicationBuilder ConfigModule{Name}(
        this WebApplicationBuilder builder,
        Action<Module{Name}Option>? configureOptions = null)
    {
        var option = new Module{Name}Option();
        configureOptions?.Invoke(option);

        builder.Services.AddSingleton(option);
        builder.Services.AddModule<Module{Name}>();

        return builder;
    }

    /// <summary>
    /// Configure {Name} module with fluent guide
    /// </summary>
    public static Module{Name}Guide ConfigModule{Name}(this WebApplicationBuilder builder)
    {
        var guide = new Module{Name}Guide();
        builder.Services.AddSingleton(guide.Option);
        builder.Services.AddModule<Module{Name}>();

        return guide;
    }
}
```

## Registration Examples

### Basic Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Option 1: With options action
builder.ConfigModuleSignalR(options =>
{
    options.EnableFeature = true;
    options.MaxItems = 50;
});

// Option 2: With fluent guide
builder.ConfigModuleSignalR()
    .EnableFeature()
    .WithMaxItems(50);
```

### With Dependencies

```csharp
// Dependencies are automatically registered
builder.ConfigModuleJobSchedulerUI(options =>
{
    options.DisableJobSchedulerPage = false;
});

// ModuleJobScheduler and ModuleUICore are automatically added
```

## Service Layer Integration

### Service Definition

Services should be defined in the source module (not UI module):

```csharp
// Interface
public interface I{Name}Service
{
    Task<Res<TResponse>> GetDataAsync(TRequest request);
    Task<Res> ExecuteActionAsync(TRequest request);
}

// Implementation
public class {Name}Service(
    ILogger<{Name}Service> logger,
    IOtherDependency dependency) : I{Name}Service
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

### Using Options in Services

```csharp
public class {Name}Service(
    IOptions<Module{Name}Option> options,
    ILogger<{Name}Service> logger) : I{Name}Service
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

## Page Route Definition

```csharp
@page "/module-name-page"
@attribute [Route({NAME}_URL)]

@code {
    public const string {NAME}_URL = "/module-name-page";
}
```

## Best Practices

1. **Use primary constructors** for dependency injection
2. **Keep modules focused** - one module, one responsibility
3. **Declare dependencies explicitly** using `DependsOnModule<TGuide>().Register()`
4. **Use options for configuration** - inject `IOptions<TOption>`
5. **Follow naming conventions** - consistent naming makes code discoverable
6. **Services in source module** - business logic belongs in the source module, not UI
7. **Return Res types** - all service methods should return `Res<T>` or `Res`
