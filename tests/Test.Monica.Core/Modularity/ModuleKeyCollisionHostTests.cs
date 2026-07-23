using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Extensions;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleKeyCollisionHostTests
{
    [Fact]
    public void AddMonica_WhenModuleTypesUseCaseEquivalentKeys_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.AddModule<CollisionAlphaModule, CollisionAlphaModuleOption, CollisionAlphaModuleGuide>();
            monica.AddModule<CollisionBetaModule, CollisionBetaModuleOption, CollisionBetaModuleGuide>();
        });

        compose.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already mapped to type*");
    }
}

[ModuleKey("Test.Monica.Core.Collision")]
public sealed class CollisionAlphaModule(CollisionAlphaModuleOption option)
    : ModuleBase<CollisionAlphaModule, CollisionAlphaModuleOption, CollisionAlphaModuleGuide>(option);

public sealed class CollisionAlphaModuleGuide
    : ModuleGuide<CollisionAlphaModule, CollisionAlphaModuleOption, CollisionAlphaModuleGuide>;

public sealed class CollisionAlphaModuleOption : ModuleOptions<CollisionAlphaModule>;

[ModuleKey("test.Monica.core.collision")]
public sealed class CollisionBetaModule(CollisionBetaModuleOption option)
    : ModuleBase<CollisionBetaModule, CollisionBetaModuleOption, CollisionBetaModuleGuide>(option);

public sealed class CollisionBetaModuleGuide
    : ModuleGuide<CollisionBetaModule, CollisionBetaModuleOption, CollisionBetaModuleGuide>;

public sealed class CollisionBetaModuleOption : ModuleOptions<CollisionBetaModule>;
