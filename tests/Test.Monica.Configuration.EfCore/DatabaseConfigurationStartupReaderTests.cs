using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Annotations;
using Monica.Configuration.Bootstrap;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task StartupReader_WhenUsingEfCoreStore_ShouldReadEffectiveOptions()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["StartupReader:Enabled"] = "true";
        IMonicaEffectiveOptionsReader? reader = null;

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            var configuration = monica.AddConfiguration();
            reader = configuration.CreateEffectiveOptionsReader(
                builder,
                new MonicaEffectiveOptionsReaderConfiguration()
                    .UseDbConfigurationStore((_, options) =>
                        ConfigurationStoreTestContextFactory.ConfigureOptions(
                            options,
                            $"Data Source={databasePath}")));
        });

        var effectiveOptionsReader = reader
            ?? throw new InvalidOperationException("Monica did not create the startup effective-options reader.");
        await using (effectiveOptionsReader)
        {
            var options = await effectiveOptionsReader.GetAsync<StartupReaderOptions>(
                TestContext.Current.CancellationToken);
            options.Enabled.Should().BeTrue();
        }
    }

    [Configuration("StartupReader", DefinitionKey = "test.configuration.startup-reader")]
    private sealed class StartupReaderOptions
    {
        public bool Enabled { get; set; }
    }
}
