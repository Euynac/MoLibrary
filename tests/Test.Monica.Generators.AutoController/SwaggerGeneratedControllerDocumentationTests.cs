using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Monica.WebApi.Swagger.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Test.Monica.Generators.AutoController;

public sealed class SwaggerGeneratedControllerDocumentationTests
{
    [Fact]
    public async Task GetSwagger_WhenGeneratedActionsUseInheritDoc_ShouldMergeCrossAssemblyDocumentation()
    {
        using var documentation = TemporaryXmlDocumentation.Create();
        await using var application = await CreateApplicationAsync(documentation.Paths);
        using var client = application.GetTestClient();
        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var contentStream = await response.Content.ReadAsStreamAsync(
            TestContext.Current.CancellationToken);
        using var swaggerDocument = await JsonDocument.ParseAsync(
            contentStream,
            cancellationToken: TestContext.Current.CancellationToken);

        var root = swaggerDocument.RootElement;
        var inheritedOperation = GetOperation(root, "/api/v1/Flight/get-alarm-flight-list");
        inheritedOperation.GetProperty("summary").GetString()
            .Should().Be("Gets the flights matching a temporary alarm type.");
        inheritedOperation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => string.Equals(
                parameter.GetProperty("name").GetString(),
                nameof(SwaggerAlarmFlightRequest.Condition),
                StringComparison.OrdinalIgnoreCase))
            .GetProperty("description")
            .GetString()
            .Should().Be("The alarm condition used to filter flights.");

        var nestedOperation = GetOperation(root, "/api/v1/Flight/nested-inheritdoc");
        nestedOperation.GetProperty("summary").GetString()
            .Should().Be("Summary inherited through a nested inheritdoc element.");

        var localOverrideOperation = GetOperation(root, "/api/v1/Flight/local-override");
        localOverrideOperation.GetProperty("summary").GetString()
            .Should().Be("Local action summary.");
        localOverrideOperation.GetProperty("description").GetString()
            .Should().Be("Local action remarks.");
        localOverrideOperation.GetProperty("responses").GetProperty("400").GetProperty("description").GetString()
            .Should().Be("Local validation failure.");
        localOverrideOperation.GetProperty("responses").GetProperty("404").GetProperty("description").GetString()
            .Should().Be("Inherited missing-flight response.");

        GetOperation(root, "/api/v1/Flight/missing-reference")
            .ValueKind.Should().Be(JsonValueKind.Object);
    }

    private static async Task<WebApplication> CreateApplicationAsync(IEnumerable<string> xmlDocumentationPaths)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(SwaggerDocumentationController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Generated controller documentation tests",
                Version = "v1"
            });
            options.IncludeXmlCommentsWithInheritDoc(
                xmlDocumentationPaths,
                includeControllerXmlComments: true);
        });

        var application = builder.Build();
        application.UseSwagger();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }

    private static JsonElement GetOperation(JsonElement document, string path)
    {
        return document.GetProperty("paths").GetProperty(path).GetProperty("get");
    }

    private sealed class TemporaryXmlDocumentation : IDisposable
    {
        private TemporaryXmlDocumentation(string directoryPath, IReadOnlyList<string> paths)
        {
            DirectoryPath = directoryPath;
            Paths = paths;
        }

        private string DirectoryPath { get; }

        public IReadOnlyList<string> Paths { get; }

        public static TemporaryXmlDocumentation Create()
        {
            var directoryPath = Path.Combine(
                Path.GetTempPath(),
                $"monica-swagger-inheritdoc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);

            var controllerPath = Path.Combine(directoryPath, "GeneratedControllers.xml");
            var protocolPath = Path.Combine(directoryPath, "Platform.Protocol.xml");
            CreateControllerDocumentation().Save(controllerPath);
            CreateProtocolDocumentation().Save(protocolPath);
            return new TemporaryXmlDocumentation(directoryPath, [controllerPath, protocolPath]);
        }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }

        private static XDocument CreateControllerDocumentation()
        {
            var controllerType = typeof(SwaggerDocumentationController);
            var alarmRequestMember = XmlCommentsNodeNameHelper.GetMemberNameForType(
                typeof(SwaggerAlarmFlightRequest));
            var nestedRequestMember = XmlCommentsNodeNameHelper.GetMemberNameForType(
                typeof(SwaggerNestedDocumentationRequest));
            var localRequestMember = XmlCommentsNodeNameHelper.GetMemberNameForType(
                typeof(SwaggerLocalDocumentationRequest));

            return CreateDocument(
                CreateMember(
                    GetMethodMemberName(controllerType, nameof(SwaggerDocumentationController.GetAlarmFlightList)),
                    new XElement("inheritdoc", new XAttribute("cref", alarmRequestMember))),
                CreateMember(
                    GetMethodMemberName(controllerType, nameof(SwaggerDocumentationController.GetNestedSummary)),
                    new XElement(
                        "summary",
                        new XElement("inheritdoc", new XAttribute("cref", nestedRequestMember)))),
                CreateMember(
                    GetMethodMemberName(controllerType, nameof(SwaggerDocumentationController.GetLocalOverride)),
                    new XElement("inheritdoc", new XAttribute("cref", localRequestMember)),
                    new XElement("summary", "Local action summary."),
                    new XElement("remarks", "Local action remarks."),
                    new XElement("response", new XAttribute("code", "400"), "Local validation failure.")),
                CreateMember(
                    GetMethodMemberName(controllerType, nameof(SwaggerDocumentationController.GetMissingReference)),
                    new XElement("inheritdoc", new XAttribute("cref", "T:Missing.Protocol.Request"))));
        }

        private static XDocument CreateProtocolDocumentation()
        {
            var alarmRequestType = typeof(SwaggerAlarmFlightRequest);
            var conditionProperty = alarmRequestType.GetProperty(nameof(SwaggerAlarmFlightRequest.Condition))!;

            return CreateDocument(
                CreateMember(
                    XmlCommentsNodeNameHelper.GetMemberNameForType(alarmRequestType),
                    new XElement("summary", "Gets the flights matching a temporary alarm type.")),
                CreateMember(
                    XmlCommentsNodeNameHelper.GetMemberNameForFieldOrProperty(conditionProperty),
                    new XElement("summary", "The alarm condition used to filter flights.")),
                CreateMember(
                    XmlCommentsNodeNameHelper.GetMemberNameForType(typeof(SwaggerNestedDocumentationRequest)),
                    new XElement("summary", "Summary inherited through a nested inheritdoc element.")),
                CreateMember(
                    XmlCommentsNodeNameHelper.GetMemberNameForType(typeof(SwaggerLocalDocumentationRequest)),
                    new XElement("summary", "Inherited request summary."),
                    new XElement("remarks", "Inherited request remarks."),
                    new XElement("response", new XAttribute("code", "400"), "Inherited validation failure."),
                    new XElement("response", new XAttribute("code", "404"), "Inherited missing-flight response.")));
        }

        private static string GetMethodMemberName(Type controllerType, string methodName)
        {
            return XmlCommentsNodeNameHelper.GetMemberNameForMethod(controllerType.GetMethod(methodName)!);
        }

        private static XDocument CreateDocument(params XElement[] members)
        {
            return new XDocument(new XElement("doc", new XElement("members", members)));
        }

        private static XElement CreateMember(string name, params object[] documentation)
        {
            return new XElement("member", new XAttribute("name", name), documentation);
        }
    }
}

[ApiController]
[Route("api/v1/Flight")]
public sealed class SwaggerDocumentationController : ControllerBase
{
    [HttpGet("get-alarm-flight-list")]
    public string GetAlarmFlightList([FromQuery] SwaggerAlarmFlightRequest request)
    {
        return request.Condition ?? string.Empty;
    }

    [HttpGet("nested-inheritdoc")]
    public string GetNestedSummary()
    {
        return string.Empty;
    }

    [HttpGet("local-override")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public string GetLocalOverride()
    {
        return string.Empty;
    }

    [HttpGet("missing-reference")]
    public string GetMissingReference()
    {
        return string.Empty;
    }
}

public sealed class SwaggerAlarmFlightRequest
{
    public string? Condition { get; init; }
}

public sealed class SwaggerNestedDocumentationRequest;

public sealed class SwaggerLocalDocumentationRequest;
