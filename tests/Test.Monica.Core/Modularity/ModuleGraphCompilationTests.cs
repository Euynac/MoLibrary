using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Extensions;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleGraphCompilationTests
{
    [Fact]
    public void AddMonica_WhenDependencyAndHostContributeOptions_ShouldApplyHostContributionLast()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<GraphDependencyModule, GraphDependencyModuleOption>(options =>
                options.Contributions.Add("host"));
            monica.AddModule<GraphConsumerModule, GraphConsumerModuleOption>();
        });

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<GraphDependencyModuleOption>>().Value;
        var observation = host.Services.GetRequiredService<GraphOptionObservation>();

        options.Contributions.Should().Equal("dependency", "host");
        observation.DependencyOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void AddMonica_WhenHardDependencyIsNotExplicitlyAdded_ShouldIncludeIt()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
            monica.AddModule<GraphConsumerModule, GraphConsumerModuleOption>());

        using var host = builder.Build();
        var moduleTypes = host.Services.GetRequiredService<MonicaApplication>()
            .Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleType)
            .ToArray();

        moduleTypes.Should().Contain(typeof(GraphConsumerModule));
        moduleTypes.Should().Contain(typeof(GraphDependencyModule));
    }

    [Fact]
    public void AddMonica_WhenOptionalOrderingTargetIsAbsent_ShouldNotIncludeIt()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
            monica.AddModule<OptionalFollowerModule, OptionalFollowerModuleOption>());

        using var host = builder.Build();
        var moduleTypes = host.Services.GetRequiredService<MonicaApplication>()
            .Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleType)
            .ToArray();

        moduleTypes.Should().Contain(typeof(OptionalFollowerModule));
        moduleTypes.Should().NotContain(typeof(OptionalTargetModule));
        host.Services.GetRequiredService<OptionalOptionObservation>().WasPresent.Should().BeFalse();
    }

    [Fact]
    public void AddMonica_WhenModuleIsExplicitlyDisabled_ShouldOmitItBeforeOptionsAreMaterialized()
    {
        var builder = Host.CreateApplicationBuilder();
        var optionsMaterialized = false;
        builder.AddMonica(monica => monica
            .AddModule<DisabledGraphModule, DisabledGraphModuleOption>(_ => optionsMaterialized = true)
            .Disable("Disabled for this host"));

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var inspection = new ModuleSystemInspectionService(application);

        optionsMaterialized.Should().BeFalse();
        application.Modules.IsRegistered(typeof(DisabledGraphModule)).Should().BeFalse();
        application.Modules.RuntimeSnapshots.Should().NotContain(
            snapshot => snapshot.ModuleType == typeof(DisabledGraphModule));
        inspection.GetRegistrationInfo().DisabledModules.Should().ContainSingle(module =>
            module.ModuleTypeName == nameof(DisabledGraphModule));
        inspection.GetModuleDetail(typeof(DisabledGraphModule))!.ConfigInfo.DisabledReason
            .Should().Be("Disabled for this host");
    }

    [Fact]
    public void AddMonica_WhenRequiredModuleIsDisabled_ShouldDisableOnlyHardDependents()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.AddModule<DisabledGraphModule, DisabledGraphModuleOption>()
                .Disable("Provider intentionally omitted");
            monica.AddModule<DisabledGraphDependentModule, DisabledGraphDependentModuleOption>();
            monica.AddModule<IndependentGraphModule, IndependentGraphModuleOption>();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var runtimeTypes = application.Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleType)
            .ToArray();
        var inspection = new ModuleSystemInspectionService(application);

        runtimeTypes.Should().Equal(typeof(IndependentGraphModule));
        inspection.GetRegistrationInfo().DisabledModules.Select(static module => module.ModuleTypeName)
            .Should().BeEquivalentTo(
                nameof(DisabledGraphModule),
                nameof(DisabledGraphDependentModule));
        inspection.GetModuleDetail(typeof(DisabledGraphDependentModule))!.ConfigInfo.DisabledReason
            .Should().Contain(nameof(DisabledGraphModule));
    }

    [Fact]
    public void AddMonica_WhenOptionalOrderingTargetIsDisabled_ShouldKeepFollowerActive()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.AddModule<OptionalTargetModule, OptionalTargetModuleOption>()
                .Disable("Optional capability omitted");
            monica.AddModule<OptionalFollowerModule, OptionalFollowerModuleOption>();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        application.Modules.IsRegistered(typeof(OptionalTargetModule)).Should().BeFalse();
        application.Modules.IsRegistered(typeof(OptionalFollowerModule)).Should().BeTrue();
    }

    [Fact]
    public void AddMonica_WhenRetainedRegistrationIsMutated_ShouldRejectMutation()
    {
        var builder = Host.CreateApplicationBuilder();
        ModuleRegistration<IndependentGraphModule, IndependentGraphModuleOption>? registration = null;
        builder.AddMonica(monica =>
            registration = monica.AddModule<IndependentGraphModule, IndependentGraphModuleOption>());

        Action mutate = () => registration!.Disable("Too late");

        mutate.Should().Throw<InvalidOperationException>()
            .WithMessage("*graph is sealed*");
    }
}

internal sealed class GraphConsumerModule : MonicaModule<GraphConsumerModuleOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<GraphDependencyModule, GraphDependencyModuleOption>(options =>
            options.Contributions.Add("dependency"));
    }

    public override void ConfigureServices(ModuleContext<GraphConsumerModuleOption> context)
    {
        var dependencyOptions = context.Modules.Get<GraphDependencyModule, GraphDependencyModuleOption>();
        context.Services.AddSingleton(new GraphOptionObservation(dependencyOptions));
    }
}

internal sealed class GraphConsumerModuleOption : ModuleOptions<GraphConsumerModule>;

internal sealed class GraphDependencyModule : MonicaModule<GraphDependencyModuleOption>;

internal sealed class GraphDependencyModuleOption : ModuleOptions<GraphDependencyModule>
{
    internal List<string> Contributions { get; } = [];
}

internal sealed record GraphOptionObservation(GraphDependencyModuleOption DependencyOptions);

internal sealed class OptionalFollowerModule : MonicaModule<OptionalFollowerModuleOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.AfterIfPresent<OptionalTargetModule, OptionalTargetModuleOption>();
    }

    public override void ConfigureServices(ModuleContext<OptionalFollowerModuleOption> context)
    {
        var wasPresent = context.Modules.TryGet<OptionalTargetModule, OptionalTargetModuleOption>(out _);
        context.Services.AddSingleton(new OptionalOptionObservation(wasPresent));
    }
}

internal sealed class OptionalFollowerModuleOption : ModuleOptions<OptionalFollowerModule>;

internal sealed record OptionalOptionObservation(bool WasPresent);

internal sealed class OptionalTargetModule : MonicaModule<OptionalTargetModuleOption>;

internal sealed class OptionalTargetModuleOption : ModuleOptions<OptionalTargetModule>;

internal sealed class DisabledGraphModule : MonicaModule<DisabledGraphModuleOption>;

internal sealed class DisabledGraphModuleOption : ModuleOptions<DisabledGraphModule>;

internal sealed class DisabledGraphDependentModule : MonicaModule<DisabledGraphDependentModuleOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<DisabledGraphModule, DisabledGraphModuleOption>();
    }
}

internal sealed class DisabledGraphDependentModuleOption : ModuleOptions<DisabledGraphDependentModule>;

internal sealed class IndependentGraphModule : MonicaModule<IndependentGraphModuleOption>;

internal sealed class IndependentGraphModuleOption : ModuleOptions<IndependentGraphModule>;
