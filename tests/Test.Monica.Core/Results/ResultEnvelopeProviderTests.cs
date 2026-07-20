using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.Results;
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

    private static byte[] CompressGzip(string value)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(Encoding.UTF8.GetBytes(value));
        }

        return output.ToArray();
    }
}
