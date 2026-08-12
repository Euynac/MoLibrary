using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Monica.Dapr.Services.Support;
using NSubstitute;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprStateQueryClientTests
{
    [Fact]
    public async Task QueryAsync_ShouldReturnRawDocumentsAndUseConfiguredSidecarIdentity()
    {
        var handler = new RecordingHandler(
            """
            {
              "results": [
                { "key": "one", "data": { "nested": { "value": 7 } }, "etag": "1", "error": "" },
                { "key": "missing", "data": null, "etag": "2", "error": "" }
              ],
              "token": "next"
            }
            """);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_HTTP_ENDPOINT"] = "http://dapr-sidecar:3500",
                ["DAPR_API_TOKEN"] = "test-token"
            })
            .Build();
        var client = new DaprStateQueryClient(factory, configuration);

        var results = await client.QueryAsync(
            "state/store",
            "{\"filter\":{}}",
            TestContext.Current.CancellationToken);

        Assert.Equal("{ \"nested\": { \"value\": 7 } }", results["one"]);
        Assert.Null(results["missing"]);
        Assert.Equal(
            new Uri("http://dapr-sidecar:3500/v1.0-alpha1/state/state%2Fstore/query"),
            handler.RequestUri);
        Assert.Equal("test-token", handler.ApiToken);
        Assert.Equal("{\"filter\":{}}", handler.RequestBody);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? ApiToken { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiToken = request.Headers.TryGetValues("dapr-api-token", out var values)
                ? values.Single()
                : null;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
