using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.StateStore.Abstractions;
using Xunit;

namespace Test.Monica.Dapr.Modules;

public sealed class ModuleStateStoreDocumentProfileTests
{
    [Fact]
    public void Composition_ShouldCompileEachDocumentProfileContributionExactlyOnce()
    {
        var durableInvocations = 0;
        var customInvocations = 0;
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddStateStore()
                .ConfigureDurableJsonProfile(options =>
                {
                    durableInvocations++;
                    options.WriteIndented = true;
                })
                .AddJsonDocumentProfile("custom", "2", options =>
                {
                    customInvocations++;
                    options.PropertyNamingPolicy = null;
                });
        });

        using var host = builder.Build();
        var profiles = host.Services.GetRequiredService<IStateDocumentProfileProvider>();

        Assert.Equal(1, durableInvocations);
        Assert.Equal(1, customInvocations);
        Assert.True(profiles.GetRequiredProfile(ModuleStateStoreOption.DURABLE_JSON_PROFILE)
            .SerializerOptions.WriteIndented);
        Assert.Null(profiles.GetRequiredProfile("custom").SerializerOptions.PropertyNamingPolicy);
        Assert.Equal("custom@2", profiles.GetRequiredProfile("custom").ContractIdentity);
    }

    [Fact]
    public void Build_TwoHosts_ShouldPublishIndependentImmutableDocumentProfiles()
    {
        using var firstHost = BuildHost(options =>
        {
            options.PropertyNamingPolicy = null;
            options.WriteIndented = true;
        });
        using var secondHost = BuildHost(options => options.MaxDepth = 16);

        var firstProfile = GetDurableProfile(firstHost);
        var secondProfile = GetDurableProfile(secondHost);

        Assert.NotSame(firstProfile, secondProfile);
        Assert.NotSame(firstProfile.SerializerOptions, secondProfile.SerializerOptions);
        Assert.True(firstProfile.SerializerOptions.IsReadOnly);
        Assert.True(secondProfile.SerializerOptions.IsReadOnly);
        Assert.Null(firstProfile.SerializerOptions.PropertyNamingPolicy);
        Assert.True(firstProfile.SerializerOptions.WriteIndented);
        Assert.Same(JsonNamingPolicy.CamelCase, secondProfile.SerializerOptions.PropertyNamingPolicy);
        Assert.Equal(16, secondProfile.SerializerOptions.MaxDepth);

        var firstCompositionInput = firstHost.Services
            .GetRequiredService<IOptions<ModuleStateStoreOption>>()
            .Value;
        firstCompositionInput.ConfigureDurableJsonProfile(options => options.MaxDepth = 1);

        Assert.Equal(0, firstProfile.SerializerOptions.MaxDepth);
        Assert.Equal(16, secondProfile.SerializerOptions.MaxDepth);
    }

    private static IHost BuildHost(Action<JsonSerializerOptions> configureProfile)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddStateStore().ConfigureDurableJsonProfile(configureProfile);
        });
        return builder.Build();
    }

    private static StateDocumentProfile GetDurableProfile(IHost host)
    {
        return host.Services
            .GetRequiredService<IStateDocumentProfileProvider>()
            .GetRequiredProfile(ModuleStateStoreOption.DURABLE_JSON_PROFILE);
    }
}
