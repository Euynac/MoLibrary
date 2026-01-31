# Monica

<p align="center">
  <img src="logo.png" alt="Monica Logo" width="200" />
</p>



<p align="center">
  <a href="https://github.com/molloryn/Monica.Docs/actions"><img src="https://github.com/molloryn/Monica.Docs/actions/workflows/static.yml/badge.svg" alt="Build Status"></a>
  <a href="https://www.nuget.org/packages?q=Monica"><img src="https://img.shields.io/nuget/v/Monica.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/molloryn/Monica/blob/main/LICENSE"><img src="https://img.shields.io/github/license/molloryn/Monica" alt="License"></a>
  <a href="https://monica.dpdns.org/"><img src="https://img.shields.io/badge/docs-online-brightgreen.svg" alt="Documentation"></a>
</p>

> ⚠️ **Development Status**: Monica is currently in internal development and undergoing rapid iteration. The API is subject to breaking changes. Documentation is being actively improved. Not recommended for production use at this time.

**Mo**dular .**N**ET **I**nfrastructure for **C**utting-edge **A**pps. A comprehensive framework providing 30+ independent modules covering everything from core infrastructure to AI integration.

## Language

English | [简体中文](README.zh_CN.md)

## 📖 Overview

Monica is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework. Through unified registration and configuration patterns, Monica provides a consistent and efficient development experience.

**[📚 Documentation](https://monica.dpdns.org/) • [🚀 Quick Start](https://monica.dpdns.org/docs/intro) • [📝 Blog](https://monica.dpdns.org/blog)**

## ✨ Features

- **🧩 True Modularity**: Each component is independent - use only what you need without pulling in the entire framework
- **🔄 Unified Intuitive API**: All modules follow the same registration and configuration patterns with the `Mo.Add*()` convention
- **⚡ High Performance**:
  - Automatic middleware registration without manual configuration
  - Prevents duplicate registrations - modules auto-register only once
  - Optimized service registration with reduced reflection overhead
  - Timely disposal of temporary objects to minimize memory footprint
- **🔌 Auto Middleware Resolution**: No need to manually manage middleware registration order
- **🔍 Dependency Visualization**: Proactive warnings for potential registration failures and misconfigurations
- **🎯 Source Generators**: Code generation for reduced boilerplate and improved performance
- **🖥️ Comprehensive Dashboards**: Built-in UI for monitoring and management
- **🌐 Distributed-First Design**: Native support for distributed systems and microservices
- **🔒 Strong Typing**: Full leverage of C# type system for compile-time safety

## 📦 Available Modules

Monica provides 30+ modules organized by category. Modules marked with ⭐ are commonly used core modules.

### Core Infrastructure
- **Core** ⭐ - Fundamental types, utilities, and base infrastructure
- **Tool** ⭐ - Common utilities and helper functions
- **DependencyInjection** - Enhanced dependency injection capabilities

### Domain-Driven Design
- **DomainDrivenDesign** - DDD pattern implementations and base classes
- **AutoController** ⭐ - Automatic API controller generation from services
- **AutoModel** - Automatic model mapping and transformation

### Data Access
- **Repository** ⭐ - Repository pattern implementation with EF Core integration
- **StateStore** - State management and persistence

### Background Processing
- **JobScheduler** ⭐ - Background job scheduling and execution with recurring and triggered jobs

### Configuration
- **Configuration** ⭐ - Enhanced configuration management with validation and hot-reload

### Communication
- **DataChannel** - Data streaming and channel-based communication
- **EventBus** - Event-driven architecture support
- **SignalR** - Real-time communication extensions
- **Dapr** ⭐ - Dapr integration for distributed applications

### Distributed Systems
- **RegisterCentre** - Service registration and discovery
- **Locker** - Distributed locking mechanisms
- **Resilience** - Resilience patterns (retry, circuit breaker, etc.)

### Security
- **Authority** - Authentication and authorization infrastructure

### AI Integration
- **AI** ⭐ - AI service integration and abstractions

### Monitoring & Observability
- **Logging** - Enhanced logging capabilities
- **Profiling** - Performance profiling and diagnostics
- **Framework** - Framework-level monitoring and metrics

### UI Components
- **UI** ⭐ - Blazor UI components and utilities (MudBlazor-based)
- **Framework.UI** - Framework UI dashboards and admin panels

### Utilities
- **Office** - Office document processing (Excel, Word, etc.)
- **Validation** - Enhanced validation framework

> 📚 For detailed module descriptions, configuration options, and usage examples, see the [online documentation](https://monica.dpdns.org/).

## 🚀 Quick Start

### Installation

Install the modules you need via NuGet:

```bash
# Install core library
dotnet add package Monica.Core

# Install repository module
dotnet add package Monica.Repository

# Install job scheduler
dotnet add package Monica.JobScheduler

# Install other modules as needed...
```

### Basic Usage

Monica uses a unified modular pattern for registering and configuring services. All modules follow the `Mo.Add*()` convention:

```csharp
using Monica;

var builder = WebApplication.CreateBuilder(args);

// Register modules with the unified Mo.Add*() pattern
Mo.AddJobScheduler(o =>
{
    o.RecurringJobDebugMode = true;
    o.TriggeredJobDebugMode = true;
})
.UseEfCoreMetadataRepository();

Mo.AddConfiguration(o =>
{
    o.EnableHotReload = true;
    o.ValidateOnStartup = true;
});

var app = builder.Build();
app.Run();
```

> 💡 Modules typically return a `ModuleGuide` object for further configuration through fluent API chaining.

## 📚 Core Concepts

### Module Pattern

Monica's architecture is built around the `MoModule` concept. Each module consists of four components:

1. **`Module{Name}Option`** - Configuration options for the module
2. **`Module{Name}Guide`** - Fluent API guide for additional configuration
3. **`Module{Name}`** - Core implementation with dependency injection and middleware setup
4. **`Module{Name}BuilderExtensions`** - User-facing extension methods (the `Mo.Add*()` methods)

### Module Registration

All modules follow a consistent registration pattern:

```csharp
Mo.Add{ModuleName}(options =>
{
    // Configure module options
})
.Use{Feature}()  // Optional: Enable specific features
.With{Provider}(); // Optional: Configure providers
```

## 🏗️ Architecture Highlights

- **Automatic Middleware Registration**: Middleware components are automatically registered in the correct order based on dependencies
- **Smart Dependency Resolution**: The framework analyzes module dependencies and provides warnings for potential issues
- **Performance Optimizations**: Reduced reflection usage, optimized service registration, and efficient resource management
- **Dashboard Integration**: Many modules include built-in dashboards for monitoring and management
- **Extensibility**: Easy to extend with custom modules following the same patterns

## 🛠️ Technology Stack

- **.NET 10.0** - Latest .NET runtime
- **ASP.NET Core** - Web framework
- **Entity Framework Core** - ORM for data access
- **MudBlazor** - Blazor UI component library
- **Mapster** - Object mapping
- **MediatR** - Mediator pattern implementation
- **Dapr** - Distributed application runtime
- **Serilog** - Structured logging
- **FluentValidation** - Validation framework
- **Polly** - Resilience and transient-fault-handling

## 📖 Documentation

- **[Online Documentation](https://monica.dpdns.org/)** - Comprehensive guides and API reference
- **Module READMEs** - Each module includes detailed documentation in its directory
- **Claude Code Skills** - Use `/mo-development` and `/mo-ui-development` skills for development guidance

## ⚠️ Development Status

**Important**: Monica is currently in active internal development:

- 🚧 **Rapid Iteration**: The API is subject to breaking changes without notice
- 📝 **Documentation**: Being actively improved and expanded
- 🔬 **Internal Use**: Currently designed for internal projects
- ⚠️ **Not Production-Ready**: Not recommended for production use at this time
- 🔄 **No Backward Compatibility**: Backward compatibility is not guaranteed during this phase

We recommend waiting for the official stable release before using Monica in production environments.

## 🤝 Contributing

We welcome contributions! To contribute:

1. Fork this repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 📞 Contact

- **GitHub Issues**: [https://github.com/molloryn/Monica/issues](https://github.com/molloryn/Monica/issues)
- **GitHub Discussions**: [https://github.com/molloryn/Monica/discussions](https://github.com/molloryn/Monica/discussions)
- **Documentation**: [https://monica.dpdns.org/](https://monica.dpdns.org/)
