using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Bootstrap;
using Monica.Core.Modularity.Extensions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task InputPlan_WhenUsedForStartupAndRuntime_ShouldTargetSameEfCoreStore()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["StartupInputPlan:Enabled"] = "true";
        var inputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
            .UseDbConfigurationStore(options =>
                ConfigurationStoreTestContextFactory.ConfigureOptions(
                    options,
                    $"Data Source={databasePath}")));
        using var bootstrap = inputPlan.BuildBootstrapConfiguration(builder);

        var snapshot = await inputPlan.EnsureEffectiveOptionsSnapshotAsync(
            bootstrap,
            [typeof(StartupInputPlanOptions)],
            cancellationToken: TestContext.Current.CancellationToken);
        snapshot.Get<StartupInputPlanOptions>().Enabled.Should().BeTrue();

        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddConfiguration(inputPlan);
        });
        builder.Services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IAuditPropertySetter, NoOpAuditPropertySetter>());
        await using var runtimeProvider = builder.Services.BuildServiceProvider();
        var runtimeStore = runtimeProvider.GetRequiredService<IConfigurationEffectiveValueStore>();

        runtimeStore.Descriptor.StoreKey.Should().Be("db:default");
        var runtimeDocument = await runtimeStore.GetAsync(
            "test.configuration.startup-input-plan",
            TestContext.Current.CancellationToken);
        runtimeDocument.Should().NotBeNull();
        var runtimeJson = JsonNode.Parse(runtimeDocument!.Json)!.AsObject();
        runtimeJson["Enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [Configuration("StartupInputPlan", DefinitionKey = "test.configuration.startup-input-plan")]
    private sealed class StartupInputPlanOptions
    {
        public bool Enabled { get; set; }
    }
}
