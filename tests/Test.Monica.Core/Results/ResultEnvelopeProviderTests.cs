using System.IO.Compression;
using System.Dynamic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.Core.Results.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Results;

public class ResultEnvelopeProviderTests
{
    [Fact]
    public async Task ReadRemoteResponse_WhenResponseIsGzipEncoded_ShouldDeserializeEnvelope()
    {
        var json = JsonSerializer.Serialize(new Res<string>("hello"));
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(CompressGzip(json))
        };
        response.Content.Headers.ContentEncoding.Add("gzip");
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };

        var reader = new ResultEnvelopeProvider(
            new JsonSerializerOptionsProvider(new JsonSerializerOptions()),
            Options.Create(new ModuleResultEnvelopeOption()),
            NullLogger<ResultEnvelopeProvider>.Instance);
        var result = await reader.ReadRemoteResponse<Res<string>>(
            response,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResStatus.Ok);
        result.Data.Should().Be("hello");
    }

    [Fact]
    public async Task ReadRemoteResponse_WhenCustomEnvelopeHasNoParameterlessConstructor_ShouldCreateFailureEnvelope()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        };
        var reader = new ResultEnvelopeProvider(
            new JsonSerializerOptionsProvider(new JsonSerializerOptions()),
            Options.Create(new ModuleResultEnvelopeOption()),
            NullLogger<ResultEnvelopeProvider>.Instance);

        var result = await reader.ReadRemoteResponse<CustomEnvelope>(
            response,
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResStatus.InternalError);
        result.Message.Should().Be("Failed to process remote API response.");
        result.Data.Should().Be(CustomEnvelope.RemoteFailureData);
    }

    [Fact]
    public async Task ReadRemoteResponse_WhenResponseReadingIsCanceled_ShouldPropagateCancellation()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var reader = new ResultEnvelopeProvider(
            new JsonSerializerOptionsProvider(new JsonSerializerOptions()),
            Options.Create(new ModuleResultEnvelopeOption()),
            NullLogger<ResultEnvelopeProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> read = async () =>
            await reader.ReadRemoteResponse<Res<string>>(response, cancellation.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    private static byte[] CompressGzip(string value)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(Encoding.UTF8.GetBytes(value));
        }

        return output.ToArray();
    }

    private sealed record CustomEnvelope(string Data) : IRemoteResultEnvelope<CustomEnvelope>
    {
        public const string RemoteFailureData = "remote-failure";

        public string? Message { get; set; }

        public ResStatus Status { get; set; }

        public ExpandoObject? Metadata { get; set; }

        public static CustomEnvelope CreateRemoteFailure(ResStatus status, string message) => new(RemoteFailureData)
        {
            Status = status,
            Message = message
        };
    }
}
