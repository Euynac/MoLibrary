using System.Collections.Immutable;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.UI.UIModuleSystem.State;

namespace Test.Monica.UI.UIModuleSystem;

internal static class ModuleSystemWorkbenchTestData
{
    internal static readonly ModuleKey AlphaKey = ModuleKey.FromModuleType(typeof(AlphaModule));
    internal static readonly ModuleKey BetaKey = ModuleKey.FromModuleType(typeof(BetaModule));

    internal static ModuleDiagnosticsSnapshot Snapshot(
        long revision = 1,
        bool isFinal = true,
        IReadOnlyList<ModuleDiagnosticFinding>? findings = null)
    {
        var alpha = Module(AlphaKey, nameof(AlphaModule), "Test.Core", 0, callbackMs: 6, dependencyCount: 0);
        var beta = Module(
            BetaKey,
            nameof(BetaModule),
            "Test.Web",
            1,
            callbackMs: 2,
            startupMs: 12,
            dependencyCount: 1,
            isWeb: true);
        return new ModuleDiagnosticsSnapshot
        {
            CompositionId = "test-composition",
            Revision = revision,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            IsFinal = isFinal,
            Outcome = isFinal ? ModuleCompositionOutcome.Succeeded : null,
            Summary = new ModuleDiagnosticsSummary
            {
                ModuleCount = 2,
                ActiveModuleCount = 2,
                TotalCompositionDurationMs = 24,
                ServiceRegistrationDurationMs = 18,
                TypeDiscoveryDurationMs = 7
            },
            Modules = [alpha, beta],
            Topology = new ModuleDiagnosticsTopology
            {
                Edges =
                [
                    new ModuleDiagnosticsDependencyEdge
                    {
                        SourceModule = BetaKey,
                        TargetModule = AlphaKey
                    }
                ],
                TopologicalOrder = [AlphaKey, BetaKey],
                MaximumDepth = 1
            },
            TraceSpans =
            [
                new ModuleDiagnosticsTraceSpan
                {
                    SpanId = "span-alpha",
                    Kind = ModuleDiagnosticsTraceSpanKind.ModuleCallback,
                    ModuleKey = AlphaKey,
                    StartedOffsetMs = 1,
                    EndedOffsetMs = 7
                }
            ],
            BlockingChain =
            [
                new ModuleBlockingChainSegment
                {
                    SegmentId = "blocking-beta",
                    BarrierSpanId = "barrier-beta",
                    WorkItemId = "work-beta",
                    WorkSpanId = "work-span-beta",
                    ModuleKey = BetaKey,
                    Barrier = ModuleStartupWorkBarrier.BeforePostConfigureServices,
                    BlockingDurationMs = 9,
                    IsBarrierReleaser = true
                }
            ],
            Findings = findings?.ToImmutableArray() ?? []
        };
    }

    internal static ModuleDiagnosticsCalls Calls(
        Func<Res<ModuleDiagnosticsSnapshot>>? snapshot = null,
        Func<Res<TypeDiscoveryAssemblyInventory>>? inventory = null,
        Func<ModuleKey, Res<ModuleOptionDiagnostics>>? options = null,
        Func<Res<ModuleDiagnosticsExport>>? export = null) => new(
        snapshot ?? (() => Res.Ok(Snapshot())),
        inventory ?? (() => Res.Ok(Inventory())),
        options ?? (key => Res.Ok(Options(key))),
        export ?? (() => Res.Ok(Export())));

    internal static TypeDiscoveryAssemblyInventory Inventory() => new()
    {
        CompositionId = "test-composition",
        UsesDefaultProjectAssemblies = true,
        Assemblies =
        [
            new TypeDiscoveryAssemblyRecord
            {
                Name = "Scanned.Assembly",
                Outcome = TypeDiscoveryAssemblyOutcome.Scanned
            },
            new TypeDiscoveryAssemblyRecord
            {
                Name = "Failed.Assembly",
                Outcome = TypeDiscoveryAssemblyOutcome.ResolutionFailed,
                Failure = "unavailable"
            }
        ]
    };

    internal static ModuleOptionDiagnostics Options(ModuleKey key) => new()
    {
        ModuleKey = key,
        OptionTypeName = "TestOptions",
        IsConfigured = true,
        Entries =
        [
            new ModuleOptionDiagnosticEntry
            {
                Name = "EndpointCount",
                Kind = ModuleOptionDiagnosticValueKind.Count,
                Count = 2
            }
        ]
    };

    internal static ModuleDiagnosticsExport Export(int schemaVersion = ModuleDiagnosticsSnapshot.CURRENT_SCHEMA_VERSION) => new()
    {
        SchemaVersion = schemaVersion,
        CompositionId = "baseline-composition",
        Revision = 1,
        IsFinal = true,
        Summary = new ModuleDiagnosticsSummary
        {
            TotalCompositionDurationMs = 20,
            TypeDiscoveryDurationMs = 5
        },
        TypeDiscovery = new ModuleDiagnosticsExportTypeDiscovery(),
        Modules =
        [
            new ModuleDiagnosticsExportModule
            {
                ModuleId = AlphaKey.Id,
                TypeName = nameof(AlphaModule),
                AssemblyName = "Test.Core",
                IsActive = true
            }
        ],
        Edges = [],
        TraceSpans = [],
        Findings = []
    };

    internal static ModuleDiagnosticsModule Module(
        ModuleKey key,
        string typeName,
        string assembly,
        int order,
        double callbackMs,
        double startupMs = 0,
        int dependencyCount = 0,
        bool isWeb = false) => new()
    {
        ModuleKey = key,
        TypeName = typeName,
        FullTypeName = key.Value,
        AssemblyName = assembly,
        RegistrationOrder = order,
        Phase = ModulePhase.PostConfigureServices,
        IsActive = true,
        IsWebModule = isWeb,
        DirectDependencyCount = dependencyCount,
        SerialCallbackDurationMs = callbackMs,
        StartupWorkDurationMs = startupMs
    };

    private sealed class AlphaModule;
    private sealed class BetaModule;
}
