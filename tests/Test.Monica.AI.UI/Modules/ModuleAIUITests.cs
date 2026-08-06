using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.AI.UI.UIChat.State;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;

namespace Test.Monica.AI.UI.Modules;

public sealed class ModuleAIUITests
{
    [Fact]
    public void Composition_ShouldRegisterScopedChatWorkspace()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options.ExcludeDefault());
            monica.AddModule<ModuleAIUI, ModuleAIUIOption>();
        });

        builder.Services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ChatSessionWorkspace)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void UseBrowserChatHistory_WhenRetentionIsInvalid_ShouldRejectConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        var configure = () => builder.AddMonica(monica =>
            monica.AddModule<ModuleAIUI, ModuleAIUIOption>()
                .UseBrowserChatHistory(options => options.MaxSessions = 0));

        configure.Should().Throw<ArgumentOutOfRangeException>();
    }
}
