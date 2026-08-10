using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.AutoModel.Abstractions;
using Monica.Core.Modularity.Abstractions;
using Monica.Modules;
using Monica.Testing.Hosting;
using Xunit;

namespace Test.Monica.AutoModel.Abstractions;

public sealed class AutoModelTestApplicationFactory : MonicaTestApplicationFactory<ModuleAutoModel>
{
    protected override void ConfigureMonica(IMonicaBuilder builder)
    {
        builder.AddAutoModel();
    }
}

public sealed class AutoModelSnapshotFactoryTests(AutoModelTestApplicationFactory applicationFactory)
    : IClassFixture<AutoModelTestApplicationFactory>
{
    [Fact]
    public async Task PreloadSnapshot_WhenModelHasNotBeenResolved_ShouldBuildAndPublishOneSnapshot()
    {
        await using var application = await applicationFactory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var factory = application.Services.GetRequiredService<IAutoModelSnapshotFactory>();

        factory.GetSnapshots().Should().BeEmpty();

        var first = factory.PreloadSnapshot<PreloadedModel>();
        var second = factory.PreloadSnapshot<PreloadedModel>();

        second.Should().BeSameAs(first);
        first.Table.FullTypeName.Should().Be(typeof(PreloadedModel).FullName);
        first.Fields.Select(field => field.ReflectionName).Should().Contain([nameof(PreloadedModel.Name), nameof(PreloadedModel.Count)]);
        factory.GetSnapshots().Should().ContainSingle().Which.Should().BeSameAs(first);
    }

    private sealed class PreloadedModel
    {
        public string Name { get; init; } = string.Empty;

        public int Count { get; init; }
    }
}
