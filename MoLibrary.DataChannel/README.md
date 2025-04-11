# MoLibrary.DataChannel

[![NuGet](https://img.shields.io/nuget/v/MoLibrary.DataChannel.svg)](https://www.nuget.org/packages/MoLibrary.DataChannel/)
[![License](https://img.shields.io/github/license/Euynac/MoLibrary.DataChannel.svg)](LICENSE)

A lightweight, flexible ETL (Extract, Transform, Load) framework for .NET applications, designed to simplify system integration through a pipeline-based data exchange approach.

## 🌟 Features

- **Template-Based Development**: Create standardized data exchange adapters quickly
- **Unified Management**: Centralized control of multiple communication channels
- **Pluggable Communication Providers**: Support for TCP, UDP, ActiveMQ, Dapr, and more
- **Extensible Pipeline Architecture**: Add custom middleware for data transformation and processing
- **Bidirectional Communication**: Support for both input and output operations in the same pipeline
- **Dashboard Integration**: Monitor channel status and configurations

## 📦 Installation

```bash
dotnet add package MoLibrary.DataChannel
```

## 🚀 Quick Start

### 1. Define Your Channels

Create a channel builder that sets up your data pipelines:

```csharp
public class ChannelBuilder : ISetupPipeline
{
    public void Setup()
    {
        // Create a pipeline using Dapr binding for Kafka
        DataPipeline.Create()
            .SetOuterEndpoint(
                new MetadataForDaprBinding(EDaprBindingType.Kafka, EConnectionDirection.Output)
                {
                    OutputBindingName = "my-output-binding"
                })
            .AddPipeMiddleware(new PipeLoggingMiddleware())
            .Register("MyProducerChannel");
            
        // Create another pipeline for input
        DataPipeline.Create()
            .SetOuterEndpoint(new MetadataForDaprBinding(EDaprBindingType.Kafka, EConnectionDirection.Input)
            {
                InputListenerRoute = "/data-sync"
            })
            .SetInnerEndpoint<MyCustomSubscriberEndpoint>()
            .Register("MySubscriberChannel");
    }
}
```

### 2. Register Services in Startup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Register DataChannel services
    services.AddDataChannel<ChannelBuilder>();
}

public void Configure(IApplicationBuilder app)
{
    // Initialize DataChannel
    app.UseDataChannel();
}
```

### 3. Use the Channel in Your Application

```csharp
public class MyService
{
    private readonly IDataChannelManager _channelManager;
    
    public MyService(IDataChannelManager channelManager)
    {
        _channelManager = channelManager;
    }
    
    public async Task SendDataAsync(object data)
    {
        var channel = _channelManager.Fetch("MyProducerChannel");
        if (channel != null)
        {
            var context = new DataContext
            {
                Operation = "send",
                Data = data,
                Entrance = EDataSource.Inner
            };
            
            await channel.Pipe.SendDataAsync(context);
        }
    }
}
```

## 🏗️ Architecture

MoLibrary.DataChannel is built around three core concepts:

### 1. DataPipeline

The main building block that defines a communication channel with:
- Inner Endpoint: Represents your application side
- Outer Endpoint: Connects to external systems
- Middleware: Components that process data as it flows through the pipeline

### 2. Endpoints

Communication interfaces that can be:
- Built-in communication cores (TCP, UDP, ActiveMQ, etc.)
- Custom implementations for specific protocols
- Default cores for special use cases

### 3. Middleware

Processing units that can:
- Transform data formats
- Apply business logic
- Log activities
- Handle special protocols (like TCP packet splitting)

## 🔄 Data Flow

Data flows through the pipeline following these steps:

1. Data enters through an endpoint (Inner or Outer)
2. Passes through transformation middleware
3. Gets processed by endpoint middleware
4. Exits through the opposite endpoint

## 🧩 Extending the Framework

### Creating Custom Communication Providers

```csharp
public class MyCustomProvider : CommunicationCore
{
    public MyCustomProvider(CommunicationMetadata metadata) : base(metadata)
    {
    }
    
    public override async Task<bool> SendDataAsync(DataContext context)
    {
        // Implementation details
    }
    
    protected override Task OnInitializeAsync()
    {
        // Connection initialization
    }
}
```

### Creating Custom Middleware

```csharp
public class MyCustomMiddleware : PipeMiddlewareBase, IPipeTransformMiddleware
{
    public async Task<DataContext> PassAsync(DataContext context)
    {
        // Transform the data
        context.Data = TransformData(context.Data);
        return context;
    }
    
    private object TransformData(object data)
    {
        // Transformation logic
    }
}
```

## 📋 Available Communication Providers

- TCP
- UDP
- ActiveMQ
- Dapr Binding (supporting Kafka, RabbitMQ, etc.)
- Default (for special cases)

## 🧰 Built-in Middleware

- `PipeLoggingMiddleware`: Logs all data passing through the pipeline
- `FilterSpecialCharacterMiddleware`: Removes or escapes special characters
- Various data transformers for common format conversions

## 📊 Dashboard Integration

MoLibrary.DataChannel includes a dashboard for monitoring:

- Active channels and their status
- Communication statistics
- Configuration settings
- Error logs

## 📚 Documentation

For more details, check out the [design document](MoDataChannel.md) and the [API reference](https://example.com/api-reference).

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 