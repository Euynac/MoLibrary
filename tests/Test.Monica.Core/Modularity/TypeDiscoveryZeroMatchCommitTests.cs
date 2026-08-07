using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class TypeDiscoveryZeroMatchCommitTests
{
    [Fact]
    public void AddMonica_WhenNonEmptyQueryMatchesNoTypes_ShouldCommitOnceWithEmptyMatches()
    {
        var probe = new ZeroMatchCommitProbe();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(TypeDiscoveryZeroMatchCommitTests).Assembly));
            monica.AddModule<ZeroMatchDiscoveryModule, ZeroMatchDiscoveryOption>(options =>
                options.Probe = probe);
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var statistics = application.Profiling.GetTypeDiscoveryStatistics();
        var query = application.Profiling.GetTypeDiscoveryQueries().Should().ContainSingle().Subject;
        var moduleKey = ModuleKey.FromModuleType(typeof(ZeroMatchDiscoveryModule));

        probe.DeclarationCount.Should().Be(1);
        probe.CommitCount.Should().Be(1);
        probe.CommittedMatchCount.Should().Be(0);
        statistics.AssemblyCount.Should().BeGreaterThan(0);
        statistics.EnumeratedTypeCount.Should().BeGreaterThan(0);
        statistics.PlanCount.Should().Be(1);
        statistics.DistinctQueryCount.Should().Be(1);
        statistics.MatchCount.Should().Be(0);
        statistics.CommitCallbackCount.Should().Be(1);
        query.MatchCount.Should().Be(0);
        query.ConsumerModules.Should().Equal(moduleKey);
        application.Profiling.GetCompositionPerformance().ModulePhaseExecutions.Should().ContainSingle(execution =>
            execution.ModuleKey == moduleKey
            && execution.Phase == ModulePhase.DeclareTypeDiscovery
            && execution.Kind == ModuleCallbackKind.TypeDiscoveryCommit);
    }
}

internal interface IZeroMatchDiscoveryMarker;

internal sealed class ZeroMatchDiscoveryModule : MonicaModule<ZeroMatchDiscoveryOption>
{
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ZeroMatchDiscoveryOption> discovery)
    {
        Option.Probe.DeclarationCount++;
        discovery.Match(
            TypeQuery.ClosedClass.AssignableTo<IZeroMatchDiscoveryMarker>(),
            (_, matches) =>
            {
                Option.Probe.CommitCount++;
                Option.Probe.CommittedMatchCount = matches.Count;
            });
    }
}

internal sealed class ZeroMatchDiscoveryOption : ModuleOptions<ZeroMatchDiscoveryModule>
{
    internal ZeroMatchCommitProbe Probe { get; set; } = null!;
}

internal sealed class ZeroMatchCommitProbe
{
    internal int DeclarationCount { get; set; }

    internal int CommitCount { get; set; }

    internal int CommittedMatchCount { get; set; }
}
