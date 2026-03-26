# Monica Framework

Monica is a **Mo**dular .**N**ET **I**nfrastructure for **C**utting-edge **A**pps. Designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

## Features

- **Modular Architecture**: Use only the modules you need
- **High Performance**: Optimized for .NET 10.0
- **Comprehensive**: Covers common infrastructure needs
- **Well Documented**: XML documentation included

## Installation

```bash
dotnet add package Monica.Core --version 0.1.0-preview.1
```

## Quick Start

```csharp
using Monica.Core;

// Your code here
```

## Available Modules

### Core Infrastructure
- **Monica.Core** - Fundamental types and base infrastructure
- **Monica.Tool** - Common utilities and helper functions
- **Monica.DependencyInjection** - Enhanced dependency injection

### Data Access
- **Monica.Repository** - Repository pattern with EF Core
- **Monica.StateStore** - State management abstractions
- **Monica.StateStore.StackExchange** - Redis state store

### Background Processing
- **Monica.JobScheduler** - Job scheduling with cron support
- **Monica.JobScheduler.EfCore** - EF Core metadata repository

### UI Components (Blazor + MudBlazor)
- **Monica.UI** - Blazor UI components
- **Monica.Framework.UI** - Framework dashboards
- **Monica.Configuration.UI** - Configuration management UI
- **Monica.JobScheduler.UI** - Job scheduler UI
- **Monica.StateStore.UI** - State store monitoring UI
- **Monica.AI.UI** - AI service management UI

### Integration
- **Monica.AI** - AI service integration (OpenAI, Anthropic)
- **Monica.Dapr** - Dapr integration
- **Monica.DataChannel** - Kafka/ActiveMQ messaging
- **Monica.EventBus** - Event bus abstractions
- **Monica.SignalR** - SignalR integration

### Infrastructure
- **Monica.Authority** - Authentication and authorization
- **Monica.AutoModel** - Automatic model generation
- **Monica.Configuration** - Configuration management
- **Monica.DomainDrivenDesign** - DDD patterns
- **Monica.Locker** - Distributed locking
- **Monica.Logging** - Structured logging with Serilog
- **Monica.Office** - Excel operations
- **Monica.Profiling** - Performance profiling
- **Monica.ServiceDiscovery** - Service discovery
- **Monica.Resilience** - Resilience patterns
- **Monica.Validation** - Validation infrastructure

### Code Generation
- **Monica.Framework.Generators** - Framework source generators
- **Monica.Generators.AutoController** - RPC controller generation
- **Monica.Generators.AutoController.Tool** - RPC metadata tool

### Meta Package
- **Monica.Framework** - Complete framework bundle

## Documentation

For detailed documentation, visit: https://github.com/molloryn/Monica

## License

MIT License - see LICENSE file for details

## Support

- Repository: https://github.com/molloryn/Monica
- Issues: https://github.com/molloryn/Monica/issues

## Preview Release

This is a preview release (0.1.0-preview.1) for early testing and feedback. APIs may change in future releases.
