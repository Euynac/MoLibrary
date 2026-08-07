using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleRegistrationStateTests
{
    [Fact]
    public void AddMonica_WhenRequiredFeatureIsMissing_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();
        var optionsMaterialized = false;

        Action compose = () => builder.AddMonica(monica =>
            monica.AddModule<RequiredFeatureProbeModule, RequiredFeatureProbeModuleOption>(_ =>
                optionsMaterialized = true));

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*RequiredFeatureProbeModule*Provider*");
        optionsMaterialized.Should().BeFalse(
            "feature requirements must be validated before option contributions execute");
    }

    [Fact]
    public void AddMonica_WhenRequiredFeatureIsSatisfied_ShouldCompleteComposition()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica => monica
            .AddModule<RequiredFeatureProbeModule, RequiredFeatureProbeModuleOption>()
            .SatisfyFeature(RequiredFeatureProbeModule.PROVIDER_FEATURE));

        using var host = builder.Build();
        host.Should().NotBeNull();
    }

    [Fact]
    public void AddMonica_WhenSatisfiedFeatureWasNeverRequired_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica => monica
            .AddModule<FeaturelessProbeModule, FeaturelessProbeModuleOption>()
            .SatisfyFeature("TypoProvider"));

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*FeaturelessProbeModule*satisfied undeclared features*TypoProvider*");
    }
}

internal sealed class RequiredFeatureProbeModule : MonicaModule<RequiredFeatureProbeModuleOption>
{
    public const string PROVIDER_FEATURE = "Provider";

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(PROVIDER_FEATURE);
    }
}

internal sealed class RequiredFeatureProbeModuleOption : ModuleOptions<RequiredFeatureProbeModule>;

internal sealed class FeaturelessProbeModule : MonicaModule<FeaturelessProbeModuleOption>;

internal sealed class FeaturelessProbeModuleOption : ModuleOptions<FeaturelessProbeModule>;
