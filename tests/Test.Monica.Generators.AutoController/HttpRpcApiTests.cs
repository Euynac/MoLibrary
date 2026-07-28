using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Converters;
using Monica.Core.JsonSerialization.Services;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.WebApi.RpcClient.Abstractions;
using Xunit;

namespace Test.Monica.Generators.AutoController;

public sealed class HttpRpcApiTests
{
    [Fact]
    public async Task CreateJsonRequestContent_WhenHostOwnsSerializerOptions_ShouldUseThoseOptions()
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        serializerOptions.Converters.Add(new DateTimeJsonConverter());

        await using var serviceProvider = new ServiceCollection()
            .AddSingleton<IJsonSerializerOptionsProvider>(
                new JsonSerializerOptionsProvider(serializerOptions))
            .BuildServiceProvider();
        using var httpClient = new HttpClient();
        var api = new ExposedHttpRpcApi(new CachedServiceProvider(serviceProvider), httpClient);
        var request = new BodyRequest(
            new DateTime(2026, 7, 28, 14, 30, 0, DateTimeKind.Utc).AddTicks(1_234_567));

        using var content = api.CreateRequestContent(request);
        var json = await content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().Be("{\"occurredAt\":\"2026-07-28T14:30:00.1234567\"}");
    }

    private sealed class ExposedHttpRpcApi(
        ICachedServiceProvider serviceProvider,
        HttpClient httpClient)
        : HttpRpcApi(serviceProvider, httpClient)
    {
        public HttpContent CreateRequestContent<TRequest>(TRequest request)
        {
            return CreateJsonRequestContent(request);
        }
    }

    private sealed record BodyRequest(DateTime OccurredAt);
}
