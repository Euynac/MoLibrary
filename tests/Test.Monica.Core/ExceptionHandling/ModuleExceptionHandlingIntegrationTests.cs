using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Results;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.ExceptionHandling;

public sealed class ModuleExceptionHandlingIntegrationTests
{
    [Fact]
    public async Task MissingRequiredJsonMember_InProduction_ReturnsStructuredBadRequest()
    {
        var endpointInvoked = false;
        await using var application = await StartApplicationAsync(() => endpointInvoked = true);

        using var response = await application.GetTestClient().PostAsync(
            "/required-json",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<Res>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        result.Should().NotBeNull();
        result!.Status.Should().Be(ResStatus.BadRequest);
        result.Message.Should().NotBeNull();
        Assert.Contains("requiredValue", result.Message!, StringComparison.OrdinalIgnoreCase);
        endpointInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task UnsupportedJsonContentType_InProduction_ReturnsStructuredUnsupportedMediaType()
    {
        var endpointInvoked = false;
        await using var application = await StartApplicationAsync(() => endpointInvoked = true);

        using var response = await application.GetTestClient().PostAsync(
            "/required-json",
            new StringContent("{}", Encoding.UTF8, "text/plain"),
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<Res>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        result.Should().NotBeNull();
        result!.Status.Should().Be(ResStatus.UnsupportedMediaType);
        result.Message.Should().NotBeNullOrWhiteSpace();
        endpointInvoked.Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ResStatus.PayloadTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ResStatus.UnsupportedMediaType)]
    public async Task EmptyFrameworkRejection_InProduction_ReturnsStructuredResponse(
        int statusCode,
        ResStatus expectedStatus)
    {
        await using var application = await StartApplicationAsync(static () => { });

        using var response = await application.GetTestClient().GetAsync(
            $"/empty-rejection/{statusCode}",
            TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<Res>(TestContext.Current.CancellationToken);

        ((int)response.StatusCode).Should().Be(statusCode);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        result.Should().NotBeNull();
        result!.Status.Should().Be(expectedStatus);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<WebApplication> StartApplicationAsync(Action onEndpointInvoked)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(ModuleExceptionHandlingIntegrationTests).Assembly));
            monica.AddExceptionHandling();
        });

        var application = builder.Build();
        application.UseMonica();
        application.MapPost("/required-json", (RequiredJsonRequest _) =>
        {
            onEndpointInvoked();
            return Microsoft.AspNetCore.Http.Results.Ok();
        });
        application.MapGet(
            "/empty-rejection/{statusCode:int}",
            (int statusCode) => Microsoft.AspNetCore.Http.Results.StatusCode(statusCode));
        application.MapMonica();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }
}

public sealed record RequiredJsonRequest
{
    public required string RequiredValue { get; init; }
}
