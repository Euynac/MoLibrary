using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Monica.AutoModel.Abstractions;
using Monica.AutoModel.Models;

namespace Monica.AutoModel.Providers;

internal sealed class SnapshotFactoryMemoryProvider(IServiceProvider serviceProvider) : IAutoModelSnapshotFactory
{
    private readonly ConcurrentDictionary<Type, AutoModelSnapshot> _snapshots = [];

    public AutoModelSnapshot PreloadSnapshot<TModel>()
    {
        _ = serviceProvider.GetRequiredService<IAutoModelSnapshot<TModel>>();
        return _snapshots.TryGetValue(typeof(TModel), out var snapshot)
            ? snapshot
            : throw new InvalidOperationException(
                $"AutoModel snapshot activation completed without registering {typeof(TModel).FullName}.");
    }

    public void Register<TModel>(AutoModelSnapshot snapshot)
    {
        if (!_snapshots.TryAdd(typeof(TModel), snapshot))
        {
            throw new InvalidOperationException($"An AutoModel snapshot for {typeof(TModel).FullName} is already registered in this host.");
        }
    }

    public IReadOnlyList<AutoModelSnapshot> GetSnapshots()
    {
        return _snapshots.Values
            .OrderBy(snapshot => snapshot.Table.FullTypeName, StringComparer.Ordinal)
            .ToArray();
    }
}
