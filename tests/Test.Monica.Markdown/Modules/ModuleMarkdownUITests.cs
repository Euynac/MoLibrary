using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Markdown.UIMarkdown.Interop;
using Monica.Markdown.UIMarkdown.State;
using Monica.Modules;

namespace Test.Monica.Markdown.Modules;

public sealed class ModuleMarkdownUITests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterFactoriesWithoutCapturingDisposablePageState()
    {
        var services = new ServiceCollection();

        new ModuleMarkdownUI(new ModuleMarkdownUIOption()).ConfigureServices(services);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(MarkdownViewerPageStateFactory)
            && descriptor.ImplementationType == typeof(MarkdownViewerPageStateFactory)
            && descriptor.Lifetime == ServiceLifetime.Transient);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(MarkdownDocumentSearchStateFactory)
            && descriptor.ImplementationType == typeof(MarkdownDocumentSearchStateFactory)
            && descriptor.Lifetime == ServiceLifetime.Transient);
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(MarkdownViewerPageState))
            .Should().BeTrue();
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(MarkdownDocumentSearchState))
            .Should().BeTrue();
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(MarkdownViewerPageStateFactory))
            .Should().BeFalse();
        typeof(IDisposable).IsAssignableFrom(typeof(MarkdownViewerPageStateFactory))
            .Should().BeFalse();
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(MarkdownDocumentSearchStateFactory))
            .Should().BeFalse();
        typeof(IDisposable).IsAssignableFrom(typeof(MarkdownDocumentSearchStateFactory))
            .Should().BeFalse();
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(MarkdownViewerPageState)
            || descriptor.ImplementationType == typeof(MarkdownViewerPageState)
            || descriptor.ServiceType == typeof(MarkdownDocumentSearchState)
            || descriptor.ImplementationType == typeof(MarkdownDocumentSearchState)
            || descriptor.ServiceType == typeof(MarkdownViewerInteropSession)
            || descriptor.ImplementationType == typeof(MarkdownViewerInteropSession));
    }
}
