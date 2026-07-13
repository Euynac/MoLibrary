using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Facades;
using Monica.AI.Chat.Providers;
using Monica.Modules;

namespace Test.Monica.AI.Modules;

public sealed class ModuleAIChatHistoryTests
{
    [Fact]
    public void ConfigureServices_WhenPersistenceIsNotConfigured_ShouldRegisterNoOpDefaults()
    {
        var services = new ServiceCollection();

        new ModuleAI(new ModuleAIOption()).ConfigureServices(services);

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
