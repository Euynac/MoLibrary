# Streaming CRUD API Best Practices and Edge Cases

## Overview

The streaming CRUD functionality provides memory-efficient data processing for large datasets using `IAsyncEnumerable<T>`. This document outlines best practices and edge cases when using the streaming APIs.

## Key Benefits

- **Memory Efficiency**: Data is processed incrementally without loading entire result sets into memory
- **Scalability**: Better performance for large datasets (millions of records)
- **Non-blocking**: Asynchronous iteration prevents thread pool starvation
- **Progressive Data Delivery**: Clients can start processing data as it becomes available

## Implementation Summary

### Core Components

1. **`IStreamingCrudAppService<T>`**: Interface for streaming CRUD operations
2. **`MoAbstractKeyCrudAppService`**: Enhanced with streaming methods
3. **`IMoMapper.ProjectToTypeStreamAsync()`**: Streaming mapper support
4. **`StreamingCrudController`**: Example controller with streaming endpoints

### New Methods Added

- `GetListStreamAsync()`: Main streaming method returning `IAsyncEnumerable<T>`
- `InnerGetListStreamAsync()`: Internal streaming implementation
- `MapToGetListOutputDtoStreamAsync()`: Single entity mapping for streaming
- `ProjectToTypeStreamAsync()`: Streaming projection in mapper

## Edge Cases and Solutions

### 1. DbContext Lifetime Management

**Problem**: DbContext disposed before enumeration completes
```csharp
// ❌ WRONG - Context may be disposed during enumeration
public async IAsyncEnumerable<Dto> GetDataStream()
{
    using var context = new MyDbContext();
    await foreach(var item in context.Entities.AsAsyncEnumerable())
    {
        yield return Map(item); // Context disposed here!
    }
}
```

**Solution**: Use dependency injection and proper scoping
```csharp
// ✅ CORRECT - Context managed by DI container
public async IAsyncEnumerable<Dto> GetDataStream(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var query = await Repository.GetQueryableAsync(); // DI-managed context
    await foreach(var item in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        yield return await MapToGetListOutputDtoStreamAsync<Dto>(item);
    }
}
```

### 2. Cancellation Token Handling

**Implementation**: Use `[EnumeratorCancellation]` attribute
```csharp
public async IAsyncEnumerable<T> GetListStreamAsync(
    TGetListInput input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var query = await CreateFilteredQueryAsync(input);
    await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested(); // Check before expensive operations
        yield return await MapToGetListOutputDtoStreamAsync<T>(entity);
    }
}
```

### 3. Error Handling in Streams

**Challenge**: Exceptions during enumeration can leave streams in inconsistent state

**Solution**: Handle errors gracefully
```csharp
public async IAsyncEnumerable<T> GetListStreamAsync(
    TGetListInput input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    IQueryable<TEntity> query = null;
    try
    {
        query = await CreateFilteredQueryAsync(input);
    }
    catch (Exception ex)
    {
        // Log error but don't yield - let exception bubble up
        Logger.LogError(ex, "Failed to create filtered query for streaming");
        throw;
    }

    await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        T result;
        try
        {
            result = await MapToGetListOutputDtoStreamAsync<T>(entity);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to map entity {EntityId} during streaming", entity.Id);
            continue; // Skip problematic entities rather than breaking the stream
        }
        yield return result;
    }
}
```

### 4. Memory Management for Large Objects

**Problem**: Individual entities may still consume significant memory

**Solution**: Consider projection at database level
```csharp
protected virtual async IAsyncEnumerable<TCustomDto> InnerGetListStreamAsync<TCustomDto>(
    TGetListInput input, 
    IQueryable<TEntity> query, 
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // For large entities, project early to reduce memory footprint
    if (typeof(TCustomDto) != typeof(TEntity) && CanProjectAtDatabaseLevel<TCustomDto>())
    {
        await foreach (var item in ObjectMapper.ProjectToTypeStreamAsync<TCustomDto>(query, cancellationToken))
        {
            yield return item;
        }
    }
    else
    {
        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return await MapToGetListOutputDtoStreamAsync<TCustomDto>(entity);
        }
    }
}
```

### 5. Client-Side Consumption

**ASP.NET Core Client**:
```csharp
// Using HttpClient with streaming
var response = await httpClient.PostAsync("/api/data/stream", content);
var stream = await response.Content.ReadAsStreamAsync();

await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<DataDto>(stream))
{
    // Process each item as it arrives
    ProcessItem(item);
}
```

**NDJSON Consumption**:
```csharp
// For NDJSON format
using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
string line;
while ((line = await reader.ReadLineAsync()) != null)
{
    var item = JsonSerializer.Deserialize<DataDto>(line);
    ProcessItem(item);
}
```

## Configuration Recommendations

### 1. JSON Serializer Configuration

Ensure System.Text.Json is configured properly:
```csharp
// In Startup.cs or Program.cs
services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultBufferSize = 4096; // Smaller buffer for streaming
});
```

### 2. HTTP Client Configuration

```csharp
services.Configure<HttpClientFactoryOptions>(options =>
{
    options.HttpClientActions.Add(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(30); // Longer timeout for streaming
    });
});
```

### 3. Controller Route Configuration

```csharp
[HttpPost("stream")]
[RequestSizeLimit(100_000_000)] // 100MB limit for large streaming requests
[RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
public IAsyncEnumerable<T> GetListStreamAsync([FromBody] TGetListInput input)
{
    return AppService.GetListStreamAsync(input);
}
```

## Performance Considerations

### When to Use Streaming

**✅ Good for:**
- Large datasets (>10,000 records)
- Real-time data processing
- Memory-constrained environments
- Long-running operations

**❌ Avoid for:**
- Small datasets (<1,000 records)
- When you need total count
- Client needs random access to data
- Traditional pagination requirements

### Monitoring and Metrics

Consider adding metrics to track streaming performance:
```csharp
public async IAsyncEnumerable<T> GetListStreamAsync(
    TGetListInput input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();
    var itemCount = 0;
    
    try
    {
        await foreach (var item in InnerGetListStreamAsync<T>(input, query, cancellationToken))
        {
            itemCount++;
            yield return item;
        }
    }
    finally
    {
        Logger.LogInformation("Streaming completed: {ItemCount} items in {ElapsedMs}ms", 
            itemCount, stopwatch.ElapsedMilliseconds);
    }
}
```

## Troubleshooting

### Common Issues

1. **"ObjectDisposedException"**: DbContext disposed too early
   - Solution: Ensure proper DI scoping and avoid manual context disposal

2. **Memory still growing**: Large individual objects
   - Solution: Use database-level projection with `ProjectToTypeStreamAsync`

3. **Client timeout**: Long-running streams
   - Solution: Increase client timeout or implement heartbeat mechanism

4. **Inconsistent results**: Concurrent modifications during streaming
   - Solution: Use appropriate isolation level or snapshot isolation

### Debug Logging

Enable detailed logging for troubleshooting:
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole()
           .AddFilter("MoLibrary.DomainDrivenDesign.AutoCrud", LogLevel.Debug)
           .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
});
```