using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.JsonSerialization;

public sealed class ModuleJsonSerializationTests
{
    [Fact]
    public void AddJsonSerialization_WhenHostDoesNotSelectAFormat_ShouldUseIso8601WallClock()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJsonSerialization();
        });

        using var host = builder.Build();
        var provider = host.Services.GetRequiredService<IJsonSerializerOptionsProvider>();

        provider.DateTimeFormat.Should().BeSameAs(DateTimeWireFormat.Iso8601WallClock);
    }

    [Fact]
    public void AddJsonSerialization_WhenHostSelectsSpaceSeparatedFormat_ShouldExposeAndApplyTheSamePolicy()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJsonSerialization(static options =>
                options.DateTimeFormat = DateTimeWireFormat.SpaceSeparatedWallClock);
        });

        using var host = builder.Build();
        var provider = host.Services.GetRequiredService<IJsonSerializerOptionsProvider>();
        var value = new DateTime(2026, 7, 28, 14, 30, 0).AddTicks(1_234_567);

        var json = JsonSerializer.Serialize(value, provider.SerializerOptions);

        provider.DateTimeFormat.Should().BeSameAs(DateTimeWireFormat.SpaceSeparatedWallClock);
        json.Should().Be("\"2026-07-28 14:30:00.1234567\"");
    }

    [Fact]
    public void ConfigureSerializer_ShouldCompileEquivalentReadOnlyCanonicalAndAspNetSnapshots()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJsonSerialization(options => options.ConfigureSerializer(serializerOptions =>
            {
                serializerOptions.WriteIndented = true;
                serializerOptions.AllowOutOfOrderMetadataProperties = true;
            }));
        });

        using var host = builder.Build();
        var canonical = host.Services.GetRequiredService<IJsonSerializerOptionsProvider>().SerializerOptions;
        var http = host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;
        var mvc = host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions;

        foreach (var options in new[] { canonical, http, mvc })
        {
            options.IsReadOnly.Should().BeTrue();
            options.WriteIndented.Should().BeTrue();
            options.AllowOutOfOrderMetadataProperties.Should().BeTrue();
            options.Converters.Select(static converter => converter.GetType())
                .Should().Equal(canonical.Converters.Select(static converter => converter.GetType()));
        }
    }

    [Fact]
    public void ConfigureSerializer_AfterComposition_ShouldRejectLateMutation()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJsonSerialization();
        });

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<ModuleJsonSerializationOption>();

        Action mutate = () => options.ConfigureSerializer(static serializerOptions =>
            serializerOptions.WriteIndented = true);

        mutate.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been compiled*late contributions*");
    }
}
