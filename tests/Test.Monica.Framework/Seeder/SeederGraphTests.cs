using AwesomeAssertions;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Annotations;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class SeederGraphTests
{
    [Fact]
    public void Create_WhenPolicyInherits_ShouldResolveHostDefaultsAndOrderByFullTypeName()
    {
        var options = new ModuleSeederOption
        {
            DefaultExecutionMode = SeederExecutionMode.Exclusive,
            DefaultCriticality = SeederCriticality.Optional,
            DefaultMaxAttempts = 4
        };

        var graph = SeederGraph.Create([typeof(ZDefaultSeeder), typeof(AExplicitSeeder)], options);

        graph.Nodes.Select(static node => node.SeederType).Should().Equal(typeof(AExplicitSeeder), typeof(ZDefaultSeeder));
        graph.Nodes[0].ExecutionMode.Should().Be(SeederExecutionMode.Concurrent);
        graph.Nodes[0].Criticality.Should().Be(SeederCriticality.Required);
        graph.Nodes[0].MaxAttempts.Should().Be(2);
        graph.Nodes[1].ExecutionMode.Should().Be(SeederExecutionMode.Exclusive);
        graph.Nodes[1].Criticality.Should().Be(SeederCriticality.Optional);
        graph.Nodes[1].MaxAttempts.Should().Be(4);
    }

    [Fact]
    public void Create_WhenDependencyIsMissing_ShouldRejectGraph()
    {
        Action create = () => SeederGraph.Create([typeof(MissingDependencyConsumer)], new ModuleSeederOption());

        create.Should().Throw<InvalidOperationException>().WithMessage("*undiscovered seeder*");
    }

    [Fact]
    public void Create_WhenSeederDependsOnItself_ShouldRejectGraph()
    {
        Action create = () => SeederGraph.Create([typeof(SelfDependentSeeder)], new ModuleSeederOption());

        create.Should().Throw<InvalidOperationException>().WithMessage("*cannot depend on itself*");
    }

    [Fact]
    public void Create_WhenDependencyIsDeclaredTwice_ShouldRejectGraph()
    {
        Action create = () => SeederGraph.Create(
            [typeof(DuplicateDependencySeeder), typeof(DependencySeeder)],
            new ModuleSeederOption());

        create.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");
    }

    [Fact]
    public void Create_WhenGraphContainsCycle_ShouldRejectGraph()
    {
        Action create = () => SeederGraph.Create(
            [typeof(CycleASeeder), typeof(CycleBSeeder)],
            new ModuleSeederOption());

        create.Should().Throw<InvalidOperationException>().WithMessage("*contains a cycle*");
    }

    [Fact]
    public void Create_WhenRequiredSeederDependsOnOptionalSeeder_ShouldRejectGraph()
    {
        Action create = () => SeederGraph.Create(
            [typeof(OptionalDependencySeeder), typeof(RequiredConsumerSeeder)],
            new ModuleSeederOption());

        create.Should().Throw<InvalidOperationException>().WithMessage("*cannot depend on optional seeder*");
    }

    [SeederPolicy(
        ExecutionMode = SeederExecutionMode.Concurrent,
        Criticality = SeederCriticality.Required,
        MaxAttempts = 2)]
    private sealed class AExplicitSeeder : NoOpSeeder;

    private sealed class ZDefaultSeeder : NoOpSeeder;

    private sealed class MissingDependencySeeder : NoOpSeeder;

    [SeederDependsOn<MissingDependencySeeder>]
    private sealed class MissingDependencyConsumer : NoOpSeeder;

    [SeederDependsOn<SelfDependentSeeder>]
    private sealed class SelfDependentSeeder : NoOpSeeder;

    private sealed class DependencySeeder : NoOpSeeder;

    [SeederDependsOn<DependencySeeder>]
    [SeederDependsOn<DependencySeeder>]
    private sealed class DuplicateDependencySeeder : NoOpSeeder;

    [SeederDependsOn<CycleBSeeder>]
    private sealed class CycleASeeder : NoOpSeeder;

    [SeederDependsOn<CycleASeeder>]
    private sealed class CycleBSeeder : NoOpSeeder;

    [SeederPolicy(Criticality = SeederCriticality.Optional)]
    private sealed class OptionalDependencySeeder : NoOpSeeder;

    [SeederDependsOn<OptionalDependencySeeder>]
    private sealed class RequiredConsumerSeeder : NoOpSeeder;

    private abstract class NoOpSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
