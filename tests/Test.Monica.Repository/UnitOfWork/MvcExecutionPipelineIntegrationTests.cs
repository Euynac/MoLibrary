using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Mediator;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.WebApi.AutoControllers;
using Monica.WebApi.AutoControllers.Annotations;
using Monica.WebApi.AutoControllers.Models;
using Xunit;

namespace Test.Monica.Repository.UnitOfWork;

public sealed class MvcExecutionPipelineIntegrationTests
{
    [Fact]
    public async Task Actions_ShouldUseExactlyOneOwningExecutionBoundary()
    {
        await using var application = await StartApplicationAsync();
        using var client = application.GetTestClient();
        var observation = application.Services.GetRequiredService<MvcPipelineObservation>();

        using var directResponse = await client.GetAsync(
            "/execution-pipeline-test/direct",
            TestContext.Current.CancellationToken);
        using var mediatedResponse = await client.GetAsync(
            "/execution-pipeline-test/mediated",
            TestContext.Current.CancellationToken);

        var directBody = await directResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var mediatedBody = await mediatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        directResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"response body: {directBody}");
        mediatedResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"response body: {mediatedBody}");
        observation.MvcStarted.Should().Be(1, "only the direct action is owned by the MVC boundary");
        observation.MvcSucceeded.Should().Be(1);
        observation.MvcFailed.Should().Be(0);
        observation.MediatorStarted.Should().Be(1, "the mediated action is owned by the request-handler boundary");
        observation.MediatorSucceeded.Should().Be(1);
        observation.MediatorFailed.Should().Be(0);
    }

    [Fact]
    public async Task UnhandledActionException_ShouldFailTheMvcExecutionBoundary()
    {
        await using var application = await StartApplicationAsync();
        using var client = application.GetTestClient();
        var observation = application.Services.GetRequiredService<MvcPipelineObservation>();

        using var response = await client.GetAsync(
            "/execution-pipeline-test/unhandled",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        observation.MvcStarted.Should().Be(1);
        observation.MvcSucceeded.Should().Be(0, "an unhandled action exception must not commit outer behaviors");
        observation.MvcFailed.Should().Be(1);
    }

    [Fact]
    public async Task HandledActionException_ShouldCompleteTheMvcExecutionBoundary()
    {
        await using var application = await StartApplicationAsync();
        using var client = application.GetTestClient();
        var observation = application.Services.GetRequiredService<MvcPipelineObservation>();

        using var response = await client.GetAsync(
            "/execution-pipeline-test/handled",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        observation.MvcStarted.Should().Be(1);
        observation.MvcSucceeded.Should().Be(1, "an inner action filter handled the exception");
        observation.MvcFailed.Should().Be(0);
    }

    private static async Task<WebApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<MvcPipelineObservation>();
        builder.Services.AddTransient<MvcExecutionPipelineTestController>();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(MvcExecutionPipelineIntegrationTests).Assembly));
            monica.AddAutoControllers();
            monica.AddMediator();
            monica.AddExecutionPipeline()
                .AddBehavior<MvcPipelineObservationBehavior>(
                    "test.mvc-observation",
                    descriptorFilter: static descriptor => descriptor.Point == MvcExecutionPoints.Action)
                .AddBehavior<MediatorPipelineObservationBehavior>(
                    "test.mediator-observation",
                    descriptorFilter: static descriptor => descriptor.Point == MediatorExecutionPoints.Request);
        });

        var application = builder.Build();
        application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (MvcPipelineTestException)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        });
        application.UseMonica();
        application.MapMonica();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }
}

[ApiController]
[Route("execution-pipeline-test")]
public sealed class MvcExecutionPipelineTestController(IMediator mediator) : ControllerBase
{
    [HttpGet("direct")]
    public IActionResult Direct()
    {
        return Ok("direct");
    }

    [HttpGet("mediated")]
    [MediatedController]
    public async Task<IActionResult> Mediated(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new MvcPipelineRequest("mediated"), cancellationToken));
    }

    [HttpGet("unhandled")]
    public IActionResult Unhandled()
    {
        throw new MvcPipelineTestException("unhandled");
    }

    [HttpGet("handled")]
    [HandleMvcPipelineTestException]
    public IActionResult Handled()
    {
        throw new MvcPipelineTestException("handled");
    }
}

public sealed record MvcPipelineRequest(string Value) : IRequest<string>;

public sealed class MvcPipelineRequestHandler : IRequestHandler<MvcPipelineRequest, string>
{
    public Task<string> Handle(MvcPipelineRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Value);
    }
}

public sealed class HandleMvcPipelineTestExceptionAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var executedContext = await next();
        if (executedContext.Exception is not MvcPipelineTestException)
        {
            return;
        }

        executedContext.ExceptionHandled = true;
        executedContext.Result = new OkObjectResult("handled");
    }
}

public sealed class MvcPipelineObservationBehavior(MvcPipelineObservation observation)
    : IExecutionBehavior<MvcActionExecutionInput, MvcActionExecutionResult>
{
    public async Task<MvcActionExecutionResult> ExecuteAsync(
        ExecutionContext<MvcActionExecutionInput> context,
        ExecutionDelegate<MvcActionExecutionResult> next)
    {
        observation.RecordMvcStarted();
        try
        {
            var result = await next();
            observation.RecordMvcSucceeded();
            return result;
        }
        catch
        {
            observation.RecordMvcFailed();
            throw;
        }
    }
}

public sealed class MediatorPipelineObservationBehavior(MvcPipelineObservation observation)
    : IExecutionBehavior<MvcPipelineRequest, string>
{
    public async Task<string> ExecuteAsync(
        ExecutionContext<MvcPipelineRequest> context,
        ExecutionDelegate<string> next)
    {
        observation.RecordMediatorStarted();
        try
        {
            var result = await next();
            observation.RecordMediatorSucceeded();
            return result;
        }
        catch
        {
            observation.RecordMediatorFailed();
            throw;
        }
    }
}

public sealed class MvcPipelineObservation
{
    private int _mvcStarted;
    private int _mvcSucceeded;
    private int _mvcFailed;
    private int _mediatorStarted;
    private int _mediatorSucceeded;
    private int _mediatorFailed;

    public int MvcStarted => Volatile.Read(ref _mvcStarted);

    public int MvcSucceeded => Volatile.Read(ref _mvcSucceeded);

    public int MvcFailed => Volatile.Read(ref _mvcFailed);

    public int MediatorStarted => Volatile.Read(ref _mediatorStarted);

    public int MediatorSucceeded => Volatile.Read(ref _mediatorSucceeded);

    public int MediatorFailed => Volatile.Read(ref _mediatorFailed);

    public void RecordMvcStarted() => Interlocked.Increment(ref _mvcStarted);

    public void RecordMvcSucceeded() => Interlocked.Increment(ref _mvcSucceeded);

    public void RecordMvcFailed() => Interlocked.Increment(ref _mvcFailed);

    public void RecordMediatorStarted() => Interlocked.Increment(ref _mediatorStarted);

    public void RecordMediatorSucceeded() => Interlocked.Increment(ref _mediatorSucceeded);

    public void RecordMediatorFailed() => Interlocked.Increment(ref _mediatorFailed);
}

public sealed class MvcPipelineTestException(string message) : Exception(message);
