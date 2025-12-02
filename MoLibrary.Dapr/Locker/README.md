# Dapr Distributed Lock Module

## Overview

Provides distributed lock functionality using Dapr's Distributed Lock API via the dedicated `DaprDistributedLockClient`. This module integrates Dapr's distributed locking capabilities into the MoLibrary framework, enabling coordination and mutual exclusion across distributed applications.

## Requirements

- Dapr 1.8 or higher
- `Dapr.DistributedLock` NuGet package (v1.16.1 or later)
- Configured Dapr lock store component

## Configuration

### Basic Setup

```csharp
builder.ConfigModuleLocker()
    .UseDaprProvider(options =>
    {
        options.StoreName = "lockstore";
        options.OwnerPrefix = "myapp-";
        options.DefaultExpirationTimeout = TimeSpan.FromMinutes(2);
    });
```

**Configuration Options:**

- **`StoreName`** (required): Name of the Dapr lock store component
- **`OwnerPrefix`** (optional): Prefix for auto-generated lock owner IDs
- **`DefaultExpirationTimeout`** (optional): Default lock expiration timeout (defaults to 2 minutes)

### Advanced Configuration

For advanced scenarios requiring custom Dapr endpoints or authentication:

```csharp
builder.ConfigModuleLocker()
    .UseDaprProvider(options =>
    {
        options.StoreName = "lockstore";

        // Custom Dapr endpoints (overrides environment variables)
        options.DaprHttpEndpoint = "http://custom-dapr:3500";
        options.DaprApiToken = "secret-token";

        // Custom gRPC options for advanced scenarios
        options.GrpcChannelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 100 * 1024 * 1024,
            ThrowOperationCanceledOnCancellation = true
        };
    });
```

**Advanced Options:**

- **`DaprHttpEndpoint`** (optional): Custom HTTP endpoint for Dapr sidecar
- **`DaprGrpcEndpoint`** (optional): Custom gRPC endpoint for Dapr sidecar
- **`DaprApiToken`** (optional): API token for Dapr authentication
- **`GrpcChannelOptions`** (optional): Custom gRPC channel configuration

### Environment Variables

If not configured explicitly, the following environment variables are used:

- `DAPR_HTTP_ENDPOINT`: HTTP endpoint of Dapr sidecar
- `DAPR_GRPC_ENDPOINT`: gRPC endpoint of Dapr sidecar
- `DAPR_HTTP_PORT`: HTTP port (if endpoint not set, defaults to 3500)
- `DAPR_GRPC_PORT`: gRPC port (if endpoint not set, defaults to 50001)
- `DAPR_API_TOKEN`: API token for authentication

This enables environment-specific configuration without code changes.

## Usage

### Basic Lock Acquisition

```csharp
public class MyService
{
    private readonly IMoDistributedLock _lock;

    public MyService(IMoDistributedLock lock)
    {
        _lock = lock;
    }

    public async Task ProcessResourceAsync(string resourceId)
    {
        // Try to acquire lock with automatic owner ID generation
        await using var handle = await _lock.TryAcquireAsync(
            name: resourceId,
            timeout: TimeSpan.FromSeconds(30)
        );

        if (handle == null)
        {
            // Failed to acquire lock within timeout
            // Another instance is processing this resource
            return;
        }

        // Critical section - only one instance can execute this
        await DoWorkAsync(resourceId);

        // Lock automatically released when handle is disposed
    }
}
```

### Lock with Custom Owner ID

```csharp
public async Task ProcessWithOwnerAsync(string resourceId, string instanceId)
{
    await using var handle = await _lock.TryAcquireAsync(
        name: resourceId,
        owner: $"instance-{instanceId}",
        timeout: TimeSpan.FromMinutes(5)
    );

    if (handle == null)
    {
        throw new InvalidOperationException(
            $"Unable to acquire lock for {resourceId}");
    }

    await DoWorkAsync(resourceId);
}
```

### Lock with Cancellation

```csharp
public async Task ProcessWithCancellationAsync(
    string resourceId,
    CancellationToken cancellationToken)
{
    await using var handle = await _lock.TryAcquireAsync(
        name: resourceId,
        timeout: TimeSpan.FromMinutes(1),
        cancellationToken: cancellationToken
    );

    if (handle == null)
    {
        // Timeout or cancellation occurred
        return;
    }

    // Perform work with cancellation support
    await DoWorkAsync(resourceId, cancellationToken);
}
```

### Key Normalization

Lock keys are automatically normalized with the configured prefix from `ModuleLockerOption`:

```csharp
builder.ConfigModuleLocker(options =>
{
    options.KeyPrefix = "myapp:locks:";
});

// Lock name "resource-123" becomes "myapp:locks:resource-123"
```

## Dapr Lock Store Configuration

Configure a Dapr lock store component (e.g., Redis):

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: lockstore
spec:
  type: lock.redis
  version: v1
  metadata:
  - name: redisHost
    value: localhost:6379
  - name: redisPassword
    value: ""
```

For other lock store implementations, see [Dapr Lock Store Components](https://docs.dapr.io/reference/components-reference/supported-locks/).

## Migration from Old API

This module previously used `DaprClient.Lock()` but has been migrated to `DaprDistributedLockClient` from the `Dapr.DistributedLock` package.

### Breaking Changes

None for consumers of the `IMoDistributedLock` interface. The migration is transparent:

- Same `IMoDistributedLock` interface
- Same method signatures
- Same behavior and semantics
- Configuration structure unchanged (only new optional properties added)

### Internal Changes

For reference, the following internal changes were made:

1. **Package**: Added `Dapr.DistributedLock` NuGet package
2. **Client Type**: `DaprClient` → `DaprDistributedLockClient`
3. **Registration**: Uses `services.AddDaprDistributedLock()` extension method
4. **Configuration**: New optional properties for advanced scenarios

### Why Migrate?

- **Alignment with Dapr**: Uses dedicated client as recommended by Dapr team
- **Future-Proof**: Old `DaprClient.Lock()` is deprecated and may be removed
- **Better Performance**: Specialized client optimized for lock operations
- **Enhanced Configuration**: More control over client behavior (endpoints, auth, gRPC options)
- **Clear Separation**: Lock operations isolated from general Dapr client concerns

## Behavior and Best Practices

### Retry Logic

The implementation includes automatic retry logic:

- Polls for lock availability every 100ms
- Continues until lock is acquired or timeout expires
- Returns `null` if timeout or cancellation occurs

### Lock Expiration

Locks automatically expire after the specified timeout:

- Prevents deadlocks if a process crashes while holding a lock
- Timeout applies to both acquisition attempt and lock hold duration
- Configure appropriate timeouts based on expected operation duration

### Owner IDs

- Auto-generated: `{OwnerPrefix}{Guid.NewGuid()}`
- Custom: Provide explicit owner ID for tracking and debugging
- Useful for identifying which instance holds a lock

### Thread Safety

- `DaprDistributedLockClient` is thread-safe and registered as singleton
- Can be safely injected and used across multiple concurrent operations
- Lock handles are NOT thread-safe - each operation should acquire its own lock

### Error Handling

```csharp
try
{
    await using var handle = await _lock.TryAcquireAsync("resource");

    if (handle == null)
    {
        // Handle timeout gracefully
        _logger.LogWarning("Unable to acquire lock for resource");
        return;
    }

    await DoWork();
}
catch (Exception ex)
{
    // Handle exceptions during locked operation
    _logger.LogError(ex, "Error during locked operation");
    throw;
}
// Lock automatically released even if exception occurs
```

## Troubleshooting

### Lock Acquisition Always Fails

1. Verify Dapr sidecar is running: `dapr --version`
2. Check lock store component is configured correctly
3. Verify lock store backend (e.g., Redis) is accessible
4. Check Dapr logs: `kubectl logs <pod-name> daprd`

### Timeout Errors

1. Increase `DefaultExpirationTimeout` in configuration
2. Reduce duration of locked operations
3. Check for deadlocks or stuck locks
4. Monitor lock contention metrics

### Authentication Errors

1. Verify `DaprApiToken` matches Dapr sidecar configuration
2. Check `DAPR_API_TOKEN` environment variable
3. Review Dapr security configuration

## Performance Considerations

- **Client Reuse**: `DaprDistributedLockClient` is registered as singleton - reused across application
- **Connection Pooling**: gRPC channels are pooled and reused efficiently
- **Retry Overhead**: 100ms polling interval trades off responsiveness vs. CPU usage
- **Lock Granularity**: Use fine-grained lock keys to minimize contention

## Additional Resources

- [Dapr Distributed Lock Documentation](https://docs.dapr.io/developing-applications/building-blocks/distributed-lock/)
- [Dapr .NET SDK Distributed Lock Guide](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-distributed-lock/)
- [Supported Lock Store Components](https://docs.dapr.io/reference/components-reference/supported-locks/)
- [MoLibrary.Locker Module](../../MoLibrary.Locker/README.md)

## Support

For issues or questions:
- File an issue in the MoLibrary repository
- Consult Dapr documentation for lock store configuration
- Review Dapr sidecar logs for diagnostic information
