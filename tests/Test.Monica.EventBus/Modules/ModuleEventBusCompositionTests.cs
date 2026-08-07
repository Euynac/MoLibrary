using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.EventBus.Modules;

public sealed class ModuleEventBusCompositionTests
{
    [Fact]
    public void Composition_WhenNoOpDistributedProviderIsSelected_ShouldValidateProviderFeature()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddEventBus(options => options.DisableAutoDiscovery = true)
                .UseNoOpDistributedEventBus();
        });

        compose.Should().NotThrow();
    }
}
