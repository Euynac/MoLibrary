using Monica.Core.Results;
using Monica.Profiling.TypeAllocation.Facades;
using Monica.Profiling.TypeAllocation.Models;

namespace Monica.Profiling.UIMemoryAnalysis.State;

public sealed class TypeAllocationPanelState(TypeAllocationFacade typeAllocationFacade)
    : IDisposable
{
    private Action? _unsubscribe;

    public event Action? StateChanged;

    public TypeAllocationSnapshot? Snapshot { get; private set; }

    public AllocationSamplingMode SelectedMode { get; set; } = AllocationSamplingMode.Low;

    public int TopN { get; set; } = 30;

    public bool IsSnapshotting { get; private set; }

    public bool ShowFullTypeName { get; set; }

    public bool IsCollecting => typeAllocationFacade.IsCollecting;

    public AllocationSamplingMode CurrentMode => typeAllocationFacade.CurrentMode;

    public Task<Res> InitializeAsync()
    {
        _unsubscribe ??= typeAllocationFacade.SubscribeToUpdates(OnSnapshotUpdated);
        return Task.FromResult(RefreshData());
    }

    public Res RefreshData()
    {
        var result = typeAllocationFacade.GetCurrentSnapshot();
        if (result.IsFailed(out var error, out var snapshot))
        {
            return Res.Fail(error.Message ?? "Failed to load type allocation snapshot.");
        }

        Snapshot = snapshot;
        NotifyStateChanged();
        return Res.Ok();
    }

    public Res StartCollection()
    {
        var result = typeAllocationFacade.StartCollection(SelectedMode);
        if (!result.IsFailed(out _))
        {
            RefreshData();
        }

        return result;
    }

    public Res StopCollection()
    {
        var result = typeAllocationFacade.StopCollection();
        if (!result.IsFailed(out _))
        {
            RefreshData();
        }

        return result;
    }

    public async Task<Res<TypeAllocationSnapshot>> TakeHeapSnapshotAsync()
    {
        if (IsSnapshotting)
        {
            return Res.Fail("A heap snapshot is already in progress.");
        }

        IsSnapshotting = true;
        NotifyStateChanged();

        try
        {
            var result = await typeAllocationFacade.TakeHeapSnapshotAsync();
            if (!result.IsFailed(out _, out var snapshot))
            {
                Snapshot = snapshot;
                NotifyStateChanged();
            }

            return result;
        }
        finally
        {
            IsSnapshotting = false;
            NotifyStateChanged();
        }
    }

    public Res ResetData()
    {
        var result = typeAllocationFacade.ResetData();
        if (!result.IsFailed(out _))
        {
            RefreshData();
        }

        return result;
    }

    public void Dispose()
    {
        _unsubscribe?.Invoke();
        _unsubscribe = null;
    }

    private void OnSnapshotUpdated(TypeAllocationSnapshot snapshot)
    {
        if (!IsCollecting)
        {
            return;
        }

        Snapshot = snapshot;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
