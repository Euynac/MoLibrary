using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Facades;
using Monica.AI.Chat.Providers;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;

namespace Test.Monica.AI.Modules;

public sealed class ModuleAIChatHistoryTests
{
    [Fact]
    public void Composition_WhenPersistenceIsNotConfigured_ShouldRegisterNoOpDefaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddAI();
        });
        var services = builder.Services;

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IChatHistoryProvider)
            && descriptor.ImplementationType == typeof(NoOpChatHistoryProvider)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IChatHistoryPartitionResolver)
            && descriptor.ImplementationType == typeof(NoOpChatHistoryPartitionResolver)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ChatHistoryFacade)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
