using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.UI.UIChat.State;
using Monica.Modules;

namespace Test.Monica.AI.UI.Modules;

public sealed class ModuleAIUITests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterScopedChatWorkspace()
    {
        var services = new ServiceCollection();

        new ModuleAIUI(new ModuleAIUIOption()).ConfigureServices(services);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ChatSessionWorkspace)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void UseBrowserChatHistory_WhenRetentionIsInvalid_ShouldRejectConfiguration()
    {
        var configure = () => new ModuleAIUIGuide().UseBrowserChatHistory(options => options.MaxSessions = 0);

        configure.Should().Throw<ArgumentOutOfRangeException>();
    }
}
