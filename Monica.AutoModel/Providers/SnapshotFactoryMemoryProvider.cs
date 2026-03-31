using Monica.AutoModel.Abstractions;
using Monica.AutoModel.Models;

namespace Monica.AutoModel.Providers;

public class SnapshotFactoryMemoryProvider : IAutoModelSnapshotFactory
{
    internal static readonly List<AutoModelSnapshot> Snapshots = [];

    public IReadOnlyList<AutoModelSnapshot> GetSnapshots()
    {
        return Snapshots;
    }
}
