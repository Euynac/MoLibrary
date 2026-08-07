using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Dapr.Modules;

public sealed class ModuleDaprStateStoreCompositionTests
{
    [Fact]
    public void Composition_WhenDaprStateStoreProviderIsSelected_ShouldValidateProviderFeature()
    {
        var builder = Host.CreateApplicationBuilder();

        var exception = Record.Exception(() => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddStateStore()
                .UseDaprStateStoreProvider(static options => options.StateStoreName = "test-state-store");
        }));

        Assert.Null(exception);
    }
}
