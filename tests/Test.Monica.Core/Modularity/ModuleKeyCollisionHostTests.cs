using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleKeyCollisionHostTests
{
    [Fact]
    public void AddMonica_WhenDistinctModuleTypesAreRegistered_ShouldUseTypeIdentity()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.AddModule<CollisionAlphaModule, CollisionAlphaModuleOption>();
            monica.AddModule<CollisionBetaModule, CollisionBetaModuleOption>();
        });

        using var host = builder.Build();
        var modules = host.Services.GetRequiredService<MonicaApplication>()
            .Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleType)
            .ToArray();

        modules.Should().Contain(typeof(CollisionAlphaModule));
        modules.Should().Contain(typeof(CollisionBetaModule));
    }
}

internal sealed class CollisionAlphaModule : MonicaModule<CollisionAlphaModuleOption>;

internal sealed class CollisionAlphaModuleOption : ModuleOptions<CollisionAlphaModule>;

internal sealed class CollisionBetaModule : MonicaModule<CollisionBetaModuleOption>;

internal sealed class CollisionBetaModuleOption : ModuleOptions<CollisionBetaModule>;
