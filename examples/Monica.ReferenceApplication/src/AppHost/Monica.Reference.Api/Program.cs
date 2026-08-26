using Monica.Configuration.Bootstrap;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

var configurationInputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
    .UseFileConfigurationStore());

builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.ProjectName = "Monica.Reference.Api";
        options.AppName = "Monica Ordering Reference";
        options.AppId = "monica-ordering-reference";
        options.DomainName = "Ordering";
    });

    monica.ConfigureModuleSystem(options =>
    {
        options.DefaultApiGroupName = "Ordering";
        options.EnableMinimalApiByDefault = true;
    });

    monica.ConfigureTypeDiscovery(options =>
        options.Add("Domains.Ordering", "Platform.Protocol"));

    monica.AddHealthCheck();
    monica.AddConfiguration(configurationInputPlan);
    monica.AddEventBus()
        .UseNoOpDistributedEventBus();
    monica.AddResultEnvelope();
    monica.AddWebApi();
    monica.AddSwagger(options =>
    {
        options.AppName = "Monica Ordering Reference";
        options.ApiVersion = "v1";
    });
    monica.AddUnitOfWork();
    monica.AddProjectUnits(options =>
    {
        options.ConventionOptions.EnableNameConvention = true;
        options.ConventionOptions.NameConventionMode = ENameConventionMode.Strict;
    });
    monica.AddJobScheduler(options =>
        {
            options.MaxWorkerExecutionThreads = 1;
        })
        .UseInMemoryStore()
        .UseSchedulerScope("monica-reference-ordering");
    monica.AddOpenTelemetry()
        .UsePrometheusEndpoint();
});

var app = builder.Build();

app.UseMonica();

app.MapGet("/", () => Results.Ok(new
{
    application = "Monica Ordering Reference",
    architecture = "Domain-first modular monolith",
    endpoints = new
    {
        orders = "/api/v1/Ordering/orders",
        approveOrder = "/api/v1/Ordering/orders/approve",
        swagger = "/swagger",
        metrics = "/metrics",
        projectUnits = "/framework/units",
        readiness = "/health",
        liveness = "/alive"
    },
    runtime = new
    {
        configuration = "Ordering options are projected from the local Monica configuration store.",
        consistency = "AutoController requests run inside a Monica unit of work.",
        approvalEvent = "EventOrderApproved is published to the local bus after unit-of-work completion.",
        scheduler = "WorkerOrderBacklogReport runs every minute with in-memory scheduler state."
    }
}));

app.MapMonica();
app.Run();
