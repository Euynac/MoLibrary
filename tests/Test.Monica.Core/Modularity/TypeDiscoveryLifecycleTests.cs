using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class TypeDiscoveryLifecycleTests
{
    private static readonly ModuleSystemStage[] EXPECTED_DISCOVERY_STAGES =
    [
        ModuleSystemStage.TypeDiscoveryPlanDeclaration,
        ModuleSystemStage.TypeDiscoveryAssemblyResolution,
        ModuleSystemStage.TypeDiscoveryTypeEnumeration,
        ModuleSystemStage.TypeDiscoveryQueryEvaluation,
        ModuleSystemStage.TypeDiscoveryRegistrationCommit
    ];

    [Fact]
    public void AddMonica_ShouldDeclareOnceDiscardEmptyPlansAndCommitZeroOrMoreMatches()
    {
        var emptyProbe = new DiscoveryProbe();
        var queryProbe = new DiscoveryProbe();
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(TypeDiscoveryLifecycleTests).Assembly));
            monica.AddModule<EmptyDiscoveryProbeModule, EmptyDiscoveryProbeOption>(options =>
                options.Probe = emptyProbe);
            monica.AddModule<QueryDiscoveryProbeModule, QueryDiscoveryProbeOption>(options =>
                    options.Probe = queryProbe)
                .ConfigureServices(_ => queryProbe.RegistrationContributionCount++);
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var performance = application.Profiling.GetCompositionPerformance();
        var statistics = application.Profiling.GetTypeDiscoveryStatistics();
        var queries = application.Profiling.GetTypeDiscoveryQueries();
        var firstCapture = application.Profiling.CaptureDiagnostics();
        var secondCapture = application.Profiling.CaptureDiagnostics();

        performance.SystemPhases
            .Where(static phase => phase.Stage is >= ModuleSystemStage.TypeDiscoveryPlanDeclaration)
            .Select(static phase => phase.Stage!.Value)
            .Should().Equal(EXPECTED_DISCOVERY_STAGES);
        performance.SystemPhases.Should().ContainSingle(phase =>
            phase.Stage == ModuleSystemStage.TypeDiscoveryTypeEnumeration);
        secondCapture.Revision.Should().Be(firstCapture.Revision);
        secondCapture.TypeDiscoveryStatistics.Should().BeSameAs(firstCapture.TypeDiscoveryStatistics);
        secondCapture.TypeDiscoveryQueries.Should().Equal(firstCapture.TypeDiscoveryQueries);

        emptyProbe.DeclarationCount.Should().Be(1);
        emptyProbe.CommitCount.Should().Be(0);
        queryProbe.DeclarationCount.Should().Be(1);
        queryProbe.CommitCount.Should().Be(1);
        queryProbe.MatchCount.Should().BeGreaterThan(0);
        queryProbe.LifecycleConfigureServicesCount.Should().Be(1);
        queryProbe.RegistrationContributionCount.Should().Be(1);

        statistics.AssemblyCount.Should().Be(1);
        statistics.EnumeratedTypeCount.Should().BeGreaterThan(0);
        statistics.PlanCount.Should().Be(1);
        statistics.DistinctQueryCount.Should().Be(1);
        statistics.MatchCount.Should().Be(queryProbe.MatchCount);
        statistics.CommitCallbackCount.Should().Be(1);
        statistics.ServiceRegistrations.AddedCount.Should().Be(1);
        statistics.ServiceRegistrations.ReplacedCount.Should().Be(1);
        statistics.ServiceRegistrations.SkippedCount.Should().Be(1);

        queries.Should().ContainSingle().Which.ConsumerModules.Should().Equal(
            ModuleKey.FromModuleType(typeof(QueryDiscoveryProbeModule)));
        performance.ModulePhaseExecutions.Should().ContainSingle(execution =>
            execution.ModuleKey == ModuleKey.FromModuleType(typeof(EmptyDiscoveryProbeModule))
            && execution.Phase == ModulePhase.DeclareTypeDiscovery
            && execution.Kind == ModuleCallbackKind.Lifecycle);
        performance.ModulePhaseExecutions.Should().ContainSingle(execution =>
            execution.ModuleKey == ModuleKey.FromModuleType(typeof(QueryDiscoveryProbeModule))
            && execution.Phase == ModulePhase.DeclareTypeDiscovery
            && execution.Kind == ModuleCallbackKind.TypeDiscoveryCommit);
        performance.ModulePhaseExecutions
            .Where(execution => execution.ModuleKey == ModuleKey.FromModuleType(typeof(QueryDiscoveryProbeModule))
                                && execution.Phase == ModulePhase.ConfigureServices)
            .Select(static execution => execution.Kind)
            .Should().Equal(ModuleCallbackKind.Lifecycle, ModuleCallbackKind.RegistrationContribution);
    }

    [Fact]
    public void Compile_WhenSeveralQueriesShareOneSnapshot_ShouldEnumerateTheSnapshotExactlyOnce()
    {
        var types = new CountingTypeSnapshot(
        [
            typeof(TypeDiscoveryLifecycleTests),
            typeof(DiscoveryProbe),
            typeof(DiscoveryRegistrationProbe)
        ]);
        var plan = new TypeDiscoveryPlan<CountingDiscoveryProbeOption>();
        plan.Match(TypeQuery.All, static (_, _) => { });
        plan.Match(TypeQuery.ClosedClass, static (_, _) => { });
        plan.Match(TypeQuery.All.AssignableTo<object>(), static (_, _) => { });

        var compilation = TypeDiscoveryCompiler.Compile(types, [(ITypeDiscoveryPlan)plan]);

        types.EnumerationCount.Should().Be(1);
        compilation.EnumeratedTypeCount.Should().Be(3);
        compilation.Queries.Should().HaveCount(3);
    }

    [Fact]
    public void AddMonica_AfterSuccessfulDiscoveryCommit_ShouldReleasePlanCallbacks()
    {
        var (host, marker) = ComposeReleaseProbe(failCommit: false);
        using (host)
        {
            AssertCollected(marker);
        }
    }

    [Fact]
    public void AddMonica_AfterFailedDiscoveryCommit_ShouldReleasePlanCallbacks()
    {
        var (_, marker) = ComposeReleaseProbe(failCommit: true);

        AssertCollected(marker);
    }

    [Fact]
    public void AddMonica_WhenOnePlanDeclaresMultipleCallbacks_ShouldCountEveryInvokedCommitCallback()
    {
        var probe = new DiscoveryProbe();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(TypeDiscoveryLifecycleTests).Assembly));
            monica.AddModule<MultiCallbackDiscoveryProbeModule, MultiCallbackDiscoveryProbeOption>(options =>
                options.Probe = probe);
        });

        using var host = builder.Build();
        var statistics = host.Services.GetRequiredService<MonicaApplication>()
            .Profiling.GetTypeDiscoveryStatistics();

        probe.CommitCount.Should().Be(2);
        statistics.PlanCount.Should().Be(1);
        statistics.DistinctQueryCount.Should().Be(1);
        statistics.CommitCallbackCount.Should().Be(2);
    }

    [Fact]
    public void AddMonica_AfterDiscoveryScheduledWorkAndCommit_ShouldReleaseEveryCapturedDelegate()
    {
        var (host, marker) = ComposeScheduledReleaseProbe();
        using (host)
        {
            AssertCollected(marker);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IHost? Host, WeakReference Marker) ComposeReleaseProbe(bool failCommit)
    {
        WeakReference? marker = null;
        var builder = Host.CreateApplicationBuilder();
        try
        {
            builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(options => options
                    .ExcludeDefault()
                    .Add(typeof(TypeDiscoveryLifecycleTests).Assembly));
                monica.AddModule<ReleaseDiscoveryProbeModule, ReleaseDiscoveryProbeOption>(options =>
                {
                    options.FailCommit = failCommit;
                    options.CaptureMarker = reference => marker = reference;
                });
            });
        }
        catch (Exception exception) when (failCommit && exception.ToString().Contains(
                                               "release-probe-commit-failure",
                                               StringComparison.Ordinal))
        {
            return (null, marker ?? throw new InvalidOperationException("The release marker was not captured."));
        }

        if (failCommit)
        {
            throw new InvalidOperationException("The failing type-discovery commit unexpectedly succeeded.");
        }

        return (
            builder.Build(),
            marker ?? throw new InvalidOperationException("The release marker was not captured."));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IHost Host, WeakReference Marker) ComposeScheduledReleaseProbe()
    {
        WeakReference? marker = null;
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(TypeDiscoveryLifecycleTests).Assembly));
            monica.AddModule<ScheduledReleaseDiscoveryProbeModule, ScheduledReleaseDiscoveryProbeOption>(options =>
                options.CaptureMarker = reference => marker = reference);
        });

        return (
            builder.Build(),
            marker ?? throw new InvalidOperationException("The scheduled release marker was not captured."));
    }

    private static void AssertCollected(WeakReference marker)
    {
        for (var attempt = 0; attempt < 5 && marker.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        marker.IsAlive.Should().BeFalse();
    }
}

internal sealed class EmptyDiscoveryProbeModule : MonicaModule<EmptyDiscoveryProbeOption>
{
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<EmptyDiscoveryProbeOption> discovery)
    {
        Option.Probe.DeclarationCount++;
    }
}

internal sealed class EmptyDiscoveryProbeOption : ModuleOptions<EmptyDiscoveryProbeModule>
{
    internal DiscoveryProbe Probe { get; set; } = null!;
}

internal sealed class QueryDiscoveryProbeModule : MonicaModule<QueryDiscoveryProbeOption>
{
    public override void ConfigureServices(ModuleContext<QueryDiscoveryProbeOption> context)
    {
        Option.Probe.LifecycleConfigureServicesCount++;
    }

    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<QueryDiscoveryProbeOption> discovery)
    {
        Option.Probe.DeclarationCount++;
        discovery.Match(TypeQuery.All, (context, matches) =>
        {
            Option.Probe.CommitCount++;
            Option.Probe.MatchCount = matches.Count;

            var descriptor = ServiceDescriptor.Singleton<DiscoveryRegistrationProbe, DiscoveryRegistrationProbe>();
            context.Registrations.TryAdd(descriptor).Should().BeTrue();
            context.Registrations.TryAdd(descriptor).Should().BeFalse();
            context.Registrations.Replace(descriptor);
        });
    }
}

internal sealed class QueryDiscoveryProbeOption : ModuleOptions<QueryDiscoveryProbeModule>
{
    internal DiscoveryProbe Probe { get; set; } = null!;
}

internal sealed class MultiCallbackDiscoveryProbeModule : MonicaModule<MultiCallbackDiscoveryProbeOption>
{
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<MultiCallbackDiscoveryProbeOption> discovery)
    {
        discovery.Match(TypeQuery.All, (_, _) => Option.Probe.CommitCount++);
        discovery.Match(TypeQuery.All, (_, _) => Option.Probe.CommitCount++);
    }
}

internal sealed class MultiCallbackDiscoveryProbeOption : ModuleOptions<MultiCallbackDiscoveryProbeModule>
{
    internal DiscoveryProbe Probe { get; set; } = null!;
}

internal sealed class ScheduledReleaseDiscoveryProbeModule
    : MonicaModule<ScheduledReleaseDiscoveryProbeOption>
{
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ScheduledReleaseDiscoveryProbeOption> discovery)
    {
        discovery.Match(TypeQuery.All, (_, _) =>
        {
            var marker = new object();
            Option.CaptureMarker(new WeakReference(marker));
            ScheduleStartupWork(
                "discovery-retention-probe",
                () => GC.KeepAlive(marker),
                () => GC.KeepAlive(marker),
                ModuleStartupWorkBarrier.BeforePostConfigureServices);
        });
    }
}

internal sealed class ScheduledReleaseDiscoveryProbeOption
    : ModuleOptions<ScheduledReleaseDiscoveryProbeModule>
{
    internal Action<WeakReference> CaptureMarker { get; set; } = null!;
}

internal sealed class ReleaseDiscoveryProbeModule : MonicaModule<ReleaseDiscoveryProbeOption>
{
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ReleaseDiscoveryProbeOption> discovery)
    {
        var marker = new object();
        Option.CaptureMarker(new WeakReference(marker));
        discovery.Match(TypeQuery.All, (_, _) =>
        {
            GC.KeepAlive(marker);
            if (Option.FailCommit)
            {
                throw new InvalidOperationException("release-probe-commit-failure");
            }
        });
    }
}

internal sealed class ReleaseDiscoveryProbeOption : ModuleOptions<ReleaseDiscoveryProbeModule>
{
    internal bool FailCommit { get; set; }

    internal Action<WeakReference> CaptureMarker { get; set; } = null!;
}

internal sealed class DiscoveryProbe
{
    internal int DeclarationCount { get; set; }

    internal int CommitCount { get; set; }

    internal int MatchCount { get; set; }

    internal int LifecycleConfigureServicesCount { get; set; }

    internal int RegistrationContributionCount { get; set; }
}

internal sealed class DiscoveryRegistrationProbe;

internal sealed class CountingDiscoveryProbeOption : IModuleOptions;

internal sealed class CountingTypeSnapshot(IReadOnlyList<Type> types) : IReadOnlyList<Type>
{
    internal int EnumerationCount { get; private set; }

    public int Count => types.Count;

    public Type this[int index] => types[index];

    public IEnumerator<Type> GetEnumerator()
    {
        EnumerationCount++;
        return types.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
