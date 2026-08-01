using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task EnsureCreatedAsync_ShouldMaterializeOnlyMissingSeedsAndPreserveInputOrder()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        await using var provider = CreateProvider(databasePath);
        var store = provider.GetRequiredService<IConfigurationEffectiveValueStore>();
        var existing = CreateDefinition("Test.Seeds.Existing");
        var missing = CreateDefinition("Test.Seeds.Missing");
        await store.EnsureCreatedAsync(
            existing,
            """{"Enabled":true}""",
            TestContext.Current.CancellationToken);
        var existingFactoryCalls = 0;
        var missingFactoryCalls = 0;

        var documents = await store.EnsureCreatedAsync(
            [
                new ConfigurationEffectiveValueSeed(
                    missing,
                    () =>
                    {
                        missingFactoryCalls++;
                        return """{"Enabled":false}""";
                    }),
                new ConfigurationEffectiveValueSeed(
                    existing,
                    () =>
                    {
                        existingFactoryCalls++;
                        return """{"Enabled":false}""";
                    })
            ],
            TestContext.Current.CancellationToken);

        missingFactoryCalls.Should().Be(1);
        existingFactoryCalls.Should().Be(0);
        documents.Select(static document => document.DefinitionKey)
            .Should().Equal(missing.DefinitionKey, existing.DefinitionKey);
        documents[0].Json.Should().Contain("false");
        documents[1].Json.Should().Contain("true");
    }
}
