using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}
