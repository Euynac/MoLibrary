using AwesomeAssertions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.State;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDependencyStateTests
{
    [Fact]
    public void RegisterMapping_WhenMappingAlreadyExists_ShouldRemainIdempotent()
    {
        var state = new ModuleDependencyState();
        var key = ModuleKey.Create("Acme.Monica.Feature");

        state.RegisterMapping(typeof(FirstModule), key);
        state.RegisterMapping(typeof(FirstModule), key);

        state.CreateModuleKeysByTypeSnapshot().Should().ContainSingle();
        state.CreateModuleTypesByKeySnapshot().Should().ContainSingle();
    }

    [Fact]
    public void RegisterMapping_WhenDifferentTypesUseCaseEquivalentKey_ShouldRejectCollision()
    {
        var state = new ModuleDependencyState();
        state.RegisterMapping(typeof(FirstModule), ModuleKey.Create("Acme.Monica.Feature"));

        var act = () => state.RegisterMapping(
            typeof(SecondModule),
            ModuleKey.Create("acme.Monica.feature"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Acme.Monica.Feature*FirstModule*SecondModule*");
    }

    [Fact]
    public void RegisterMapping_WhenTypeIsRemappedToDifferentKey_ShouldRejectRemapping()
    {
        var state = new ModuleDependencyState();
        state.RegisterMapping(typeof(FirstModule), ModuleKey.Create("Acme.Monica.First"));

        var act = () => state.RegisterMapping(
            typeof(FirstModule),
            ModuleKey.Create("Acme.Monica.Second"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FirstModule*Acme.Monica.First*Acme.Monica.Second*");
    }

    private sealed class FirstModule;

    private sealed class SecondModule;
}
