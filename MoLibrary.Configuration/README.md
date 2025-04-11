# MoLibrary.Configuration

[![NuGet](https://img.shields.io/nuget/v/MoLibrary.Configuration.svg)](https://www.nuget.org/packages/MoLibrary.Configuration/)
[![License](https://img.shields.io/github/license/Euynac/MoLibrary.Configuration.svg)](LICENSE)

A powerful configuration framework for .NET applications that enhances ASP.NET Core's native Options Pattern with attribute-based configuration registration, hot reloading, and dashboard capabilities.

## 🌟 Features

- **Attribute-Based Registration**: Simplify configuration by decorating classes with `[Configuration]` attributes
- **Hot Configuration**: Supports runtime configuration changes with Dapr and file-based providers
- **Dashboard Integration**: Built-in endpoints for monitoring and managing configurations
- **Validation Support**: Leverages data annotations for configuration validation
- **File Generation**: Automatically generates per-option configuration files for easier management
- **Options Pattern Compatible**: Works with native ASP.NET Core's Options Pattern (IOptions, IOptionsSnapshot, IOptionsMonitor)

## 📦 Installation

```bash
dotnet add package MoLibrary.Configuration
```

## 🚀 Quick Start

### 1. Define Configuration Classes

Define your configuration classes using the `[Configuration]` and `[OptionSetting]` attributes:

```csharp
[Configuration(Title = "Application Settings")]
public class AppSettings
{
    [OptionSetting(Title = "Application Name")]
    [MaxLength(10), DefaultValue("Unknown")]
    public string AppName { get; set; }
    
    [ConfigurationKeyName("AppVersion")]
    public int Version { get; set; }
    
    [OptionSetting(Title = "Hot Configuration Test Value")]
    [MaxLength(5), RegularExpression("[a-zA-Z]+")]
    public string HotConfigTest { get; set; }
}
```

### 2. Register in Startup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Register MoConfiguration
    services.AddMoConfiguration(Configuration, setting =>
    {
        setting.ErrorOnNoTagConfigAttribute = true;
        setting.EnableLoggingWithoutOptionSetting = true;
        setting.ErrorOnNoTagOptionAttribute = true;
        setting.ConfigurationAssemblyLocation = ["YourDomain", "YourService.Domain"];
        setting.GenerateFileForEachOption = true;
        setting.GenerateOptionFileParentDirectory = "configs";
        setting.SetOtherSourceAction = manager =>
        {
            manager.AddJsonFile("configs/global-settings.json", false, true);
            manager.AddJsonFile("appsettings.json", true, true);
        };
    });
}

public void Configure(IApplicationBuilder app)
{
    // Add MoConfiguration endpoints for dashboard
    app.UseEndpointsMoConfiguration();
}
```

### 3. Consume Configuration

Use standard ASP.NET Core dependency injection to consume your configurations:

```csharp
public class MyService
{
    private readonly AppSettings _settings;
    
    // Use IOptions for one-time configuration loading
    public MyService(IOptions<AppSettings> options)
    {
        _settings = options.Value;
    }
}
```

For hot configuration updates, use `IOptionsSnapshot` or `IOptionsMonitor`:

```csharp
public class MyHotConfigService
{
    private readonly IOptionsMonitor<AppSettings> _settings;
    
    public MyHotConfigService(IOptionsMonitor<AppSettings> settings)
    {
        _settings = settings;
        
        // Register for changes
        _settings.OnChange(newSettings =>
        {
            Console.WriteLine($"Configuration changed: {newSettings.AppName}");
        });
    }
    
    public void DoSomething()
    {
        // Always get current value
        var currentAppName = _settings.CurrentValue.AppName;
    }
}
```

## 📋 Attribute System

### Configuration Attribute

Applied to classes to mark them as configuration containers:

```csharp
[Configuration(Title = "Flight Options")]
public class FlightOptions
{
    // Properties...
}
```

**Properties:**
- `Title`: Friendly name for dashboard display
- `Section`: Custom section name (defaults to class name)
- `Description`: Description for documentation
- `IsOffline`: Indicates restart is required for changes to take effect
- `DisableSection`: Treats properties as isolated key-value pairs

### OptionSetting Attribute

Applied to properties to provide metadata about configuration options:

```csharp
[OptionSetting(Title = "Airport Codes")]
public List<string>? Airports { get; set; }
```

**Properties:**
- `Title`: Friendly name for dashboard display
- `Description`: Detailed description of the option
- `LoggingFormat`: Custom logging format for value changes
- `IsOffline`: Indicates restart is required for changes to take effect

## 🔄 Configuration Sources

MoConfiguration supports all standard ASP.NET Core configuration providers, with the following default priority:

1. Command-line arguments
2. Environment variables
3. User secrets (Development environment)
4. `appsettings.{Environment}.json`
5. `appsettings.json`
6. Host configuration

In addition, MoConfiguration supports:

- **Dapr Configuration**: For distributed system configuration
- **Per-Option JSON Files**: Generated for each configuration class

## 🛠️ Advanced Features

### Hot Configuration

MoConfiguration supports runtime configuration changes via:

1. File-based providers with change tracking
2. Dapr Configuration provider

### Auto-Generated Configuration Files

Enable file generation to maintain separate configuration files for each option class:

```csharp
services.AddMoConfiguration(Configuration, settings => 
{
    settings.GenerateFileForEachOption = true;
    settings.GenerateOptionFileParentDirectory = "configs";
});
```

### Dashboard Integration

MoConfiguration provides built-in API endpoints for monitoring and managing configurations:

```csharp
app.UseEndpointsMoConfiguration("ConfigurationAPI");
```

### Validation

Leverage standard .NET data annotations for validation:

```csharp
[OptionSetting(Title = "Application Name")]
[MaxLength(10), DefaultValue("Unknown")]
public string AppName { get; set; }
```

## 📚 Documentation

For more details, check out the [design document](MoConfiguration.md) and the [API reference](https://example.com/api-reference).

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 