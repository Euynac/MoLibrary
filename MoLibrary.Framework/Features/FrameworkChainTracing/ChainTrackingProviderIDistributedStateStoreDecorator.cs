using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoChainTracing;
using MoLibrary.StateStore;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Interfaces;

namespace MoLibrary.Framework.Features.FrameworkChainTracing;
public class ChainTrackingProviderILocalStateStoreDecorator(IDistributedStateStore stateStore, IMoChainTracing chainTracing, ILogger<ChainTrackingProviderIDistributedStateStoreDecorator> logger) : ChainTrackingProviderIMoStateStoreDecorator(stateStore, chainTracing, logger),
    IMemoryStateStore
{
    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, bool removePrefix = true, bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix, bool removePrefix = true, bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
public class ChainTrackingProviderIDistributedStateStoreDecorator(IDistributedStateStore stateStore, IMoChainTracing chainTracing, ILogger<ChainTrackingProviderIDistributedStateStoreDecorator> logger) : ChainTrackingProviderIMoStateStoreDecorator(stateStore, chainTracing, logger),
    IDistributedStateStore
{
    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, bool removePrefix = true, bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, string>> GetBulkStateAsync(IReadOnlyList<string> keys, string? prefix, bool removePrefix = true, bool removeEmptyValue = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, T?>> QueryStateAsync<T>(Func<QueryBuilder<T>, IFinishedQueryBuilder<T>> query, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetStateAsync(string key, string? prefix, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}