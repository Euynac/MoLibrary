using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Monica.Core.Results;
using Monica.Core.Results.Services;
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

        var result = await ResultEnvelopeProvider.ReadRemoteResponse<Res<string>>(response);

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
