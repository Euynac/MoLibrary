using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class XmlDocumentationGenericHostTests
{
    [Fact]
    public void Build_WhenUsingGenericHost_ShouldRetainXmlDocumentationServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddXmlDocumentation();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        host.Services.GetRequiredService<IXmlDocumentationService>().Should().NotBeNull();
        application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(ModuleXmlDocumentation)
            && snapshot.IsWebModule
            && !snapshot.RequiresWebHost);
    }
}
