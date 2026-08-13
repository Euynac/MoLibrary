using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Results;

public sealed class ModuleResultEnvelopeTests
{
    [Fact]
    public async Task UseResultFieldNames_ShouldApplyToEveryHostSerializerAndRemoteResponses()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddResultEnvelope().UseResultFieldNames(static names => names.Status = "code");
        });

        using var host = builder.Build();
        var canonicalOptions = host.Services
            .GetRequiredService<IJsonSerializerOptionsProvider>()
            .SerializerOptions;
        var httpOptions = host.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;
        var mvcOptions = host.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions;

        foreach (var serializerOptions in new[] { canonicalOptions, httpOptions, mvcOptions })
        {
            serializerOptions.IsReadOnly.Should().BeTrue();
            var json = JsonSerializer.Serialize(Res.Ok("ready"), serializerOptions);
            using var document = JsonDocument.Parse(json);

            document.RootElement.GetProperty("code").GetInt32().Should().Be((int)ResStatus.Ok);
            document.RootElement.TryGetProperty("status", out _).Should().BeFalse();

            var deserialized = JsonSerializer.Deserialize<Res<string>>(
                """{"message":"","code":200,"data":"hello"}""",
                serializerOptions);
            deserialized.Should().NotBeNull();
            deserialized!.Status.Should().Be(ResStatus.Ok);
            deserialized.Data.Should().Be("hello");
        }

        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "http://message/api/v1/Message/test"),
            Content = new StringContent(
                """{"message":"","code":200,"data":"hello"}""",
                Encoding.UTF8,
                "application/json")
        };
        var reader = host.Services.GetRequiredService<IResultEnvelopeReader>();

        var remoteResult = await reader.ReadRemoteResponse<Res<string>>(
            response,
            TestContext.Current.CancellationToken);

        remoteResult.Status.Should().Be(ResStatus.Ok);
        remoteResult.Data.Should().Be("hello");
        remoteResult.Message.Should().BeEmpty();
        remoteResult.Metadata.Should().BeNull();
    }
}
