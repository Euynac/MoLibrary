using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Extensions;
using Monica.Dapr.Services;
using Monica.Modules;
using Monica.StateStore.Abstractions;
using Xunit;

namespace Test.Monica.Dapr.Modules;

public sealed class ModuleDaprStateStoreCompositionTests
{
    [Fact]
    public void Build_WhenDaprStateStoreProviderIsSelected_ShouldResolveSdkNativeProvider()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddStateStore()
                .UseDaprStateStoreProvider(static options => options.StateStoreName = "test-state-store");
        });

        using var host = builder.Build();
        var provider = host.Services.GetRequiredService<IDistributedStateStore>();

        Assert.IsType<DaprStateStoreProvider>(provider);
    }
}
