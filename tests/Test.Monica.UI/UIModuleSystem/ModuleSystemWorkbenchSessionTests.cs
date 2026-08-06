using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Results;
using Monica.UI.UIModuleSystem.State;
using Monica.UI.UIModuleSystem.Support;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModuleSystemWorkbenchSessionTests
{
    [Fact]
    public async Task Refresh_failure_preserves_the_previous_immutable_snapshot()
    {
        var first = ModuleSystemWorkbenchTestData.Snapshot(revision: 7);
        var calls = 0;
        await using var session = new ModuleSystemWorkbenchSession(
            ModuleSystemWorkbenchTestData.Calls(snapshot: () => ++calls == 1
                ? Res.Ok(first)
                : Res.Fail("refresh failed")));

        await session.InitializeAsync();
        await session.RefreshAsync();

        session.Snapshot.Should().BeSameAs(first);
        session.LoadState.Should().Be(ModuleSystemWorkbenchLoadState.Ready);
        session.RefreshError.Should().Be("refresh failed");
    }

    [Fact]
    public async Task Older_snapshot_completion_cannot_replace_a_newer_request()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        var older = session.BeginSnapshotRequest(isInitial: true);
        var newer = session.BeginSnapshotRequest(isInitial: true);

        session.CompleteSnapshotRequest(older, Res.Ok(ModuleSystemWorkbenchTestData.Snapshot(1)))
            .Should().BeFalse();
        session.CompleteSnapshotRequest(newer, Res.Ok(ModuleSystemWorkbenchTestData.Snapshot(2)))
            .Should().BeTrue();

        session.Snapshot!.Revision.Should().Be(2);
    }

    [Fact]
    public async Task Baseline_import_rejects_other_schema_versions_and_compares_compatible_exports()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();

        await ImportAsync(session, ModuleSystemWorkbenchTestData.Export(schemaVersion: 99));
        session.BaselineError.Should().Be(ModuleDiagnosticsBaselineError.IncompatibleSchema);
        session.Comparison.Should().BeNull();

        await ImportAsync(session, ModuleSystemWorkbenchTestData.Export());
        session.BaselineError.Should().Be(ModuleDiagnosticsBaselineError.None);
        session.Comparison.Should().NotBeNull();
        session.Comparison!.TotalDurationDeltaMs.Should().Be(4);
        session.Comparison.AddedModules.Should().ContainSingle().Which.Should().Contain("BetaModule");
        session.Comparison.AddedEdges.Should().ContainSingle().Which.Should().Be(
            $"{ModuleSystemWorkbenchTestData.BetaKey.Id} -> {ModuleSystemWorkbenchTestData.AlphaKey.Id}");
    }

    [Fact]
    public async Task Baseline_import_rejects_a_schema_compatible_but_structurally_invalid_export()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        var module = ModuleSystemWorkbenchTestData.Export().Modules[0];
        var invalid = ModuleSystemWorkbenchTestData.Export() with
        {
            Modules = [module, module]
        };

        await ImportAsync(session, invalid);

        session.BaselineError.Should().Be(ModuleDiagnosticsBaselineError.InvalidJson);
        session.Comparison.Should().BeNull();
    }

    [Fact]
    public async Task Baseline_import_rejects_an_export_without_an_explicit_schema_version()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        var document = JsonNode.Parse(JsonSerializer.Serialize(ModuleSystemWorkbenchTestData.Export()))!.AsObject();
        document.Remove(nameof(ModuleDiagnosticsExport.SchemaVersion)).Should().BeTrue();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));

        await session.ImportBaselineAsync(stream, TestContext.Current.CancellationToken);

        session.BaselineError.Should().Be(ModuleDiagnosticsBaselineError.InvalidJson);
        session.Comparison.Should().BeNull();
    }

    [Fact]
    public async Task Baseline_import_rejects_a_sparse_document_missing_required_sections()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        const string sparseJson = """
                                  { "schemaVersion": 2, "compositionId": "sparse-baseline" }
                                  """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sparseJson));

        await session.ImportBaselineAsync(stream, TestContext.Current.CancellationToken);

        session.BaselineError.Should().Be(ModuleDiagnosticsBaselineError.InvalidJson);
        session.Comparison.Should().BeNull();
    }

    [Fact]
    public async Task Filters_selection_drawer_and_lazy_diagnostics_share_one_session()
    {
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();

        session.SetModuleFilters(
            "Beta",
            ModuleCatalogStateFilter.Active,
            ModuleCatalogCapabilityFilter.Web,
            "Test.Web",
            onlyWithDependencies: true,
            minimumCostMs: 10);
        var selected = session.PagedModules.Should().ContainSingle().Subject;
        session.SelectModule(selected);
        session.IsModuleDrawerOpen.Should().BeTrue();

        await session.EnsureSelectedModuleOptionsLoadedAsync();
        await session.EnsureAssemblyInventoryLoadedAsync();

        session.SelectedModuleOptionsState.Should().Be(ModuleSystemLazyLoadState.Ready);
        session.SelectedModuleOptions!.Entries.Should().ContainSingle();
        session.AssemblyInventoryState.Should().Be(ModuleSystemLazyLoadState.Ready);
        session.FilteredAssemblies.First().Outcome.Should().Be(TypeDiscoveryAssemblyOutcome.ResolutionFailed);
    }

    [Fact]
    public async Task Authorization_is_rechecked_before_every_facade_boundary()
    {
        var authorized = true;
        var snapshotCalls = 0;
        var inventoryCalls = 0;
        await using var session = new ModuleSystemWorkbenchSession(
            ModuleSystemWorkbenchTestData.Calls(
                snapshot: () =>
                {
                    snapshotCalls++;
                    return Res.Ok(ModuleSystemWorkbenchTestData.Snapshot());
                },
                inventory: () =>
                {
                    inventoryCalls++;
                    return Res.Ok(ModuleSystemWorkbenchTestData.Inventory());
                }),
            authorize: () => Task.FromResult(authorized));

        await session.InitializeAsync();
        authorized = false;
        await session.RefreshAsync();
        await session.EnsureAssemblyInventoryLoadedAsync();

        snapshotCalls.Should().Be(1);
        inventoryCalls.Should().Be(0);
        session.IsAccessDenied.Should().BeTrue();
        session.AssemblyInventoryState.Should().Be(ModuleSystemLazyLoadState.Failed);
    }

    [Fact]
    public async Task Polling_stops_as_soon_as_a_final_snapshot_is_observed()
    {
        var calls = 0;
        await using var session = new ModuleSystemWorkbenchSession(
            ModuleSystemWorkbenchTestData.Calls(snapshot: () => Res.Ok(
                ModuleSystemWorkbenchTestData.Snapshot(++calls, isFinal: calls >= 2))),
            pollingInterval: TimeSpan.FromMilliseconds(5));

        await session.InitializeAsync();
        await WaitUntilAsync(() => calls >= 2);
        var terminalCalls = calls;
        await Task.Delay(30, TestContext.Current.CancellationToken);

        session.Snapshot!.IsFinal.Should().BeTrue();
        calls.Should().Be(terminalCalls);
    }

    private static async Task ImportAsync(
        ModuleSystemWorkbenchSession session,
        ModuleDiagnosticsExport export)
    {
        var json = JsonSerializer.Serialize(export);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await session.ImportBaselineAsync(stream, TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < timeout)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        condition().Should().BeTrue();
    }
}
