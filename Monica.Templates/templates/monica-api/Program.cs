using Monica.Core.Modularity.Extensions;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.ProjectName = "MonicaStarter";
        options.AppName = "MonicaStarter";
    });

    monica.ConfigureModuleSystem(options =>
    {
        options.DefaultApiGroupName = "MonicaStarter";
        options.EnableMinimalApiByDefault = true;
    });

    monica.AddHealthCheck();
    monica.AddOpenTelemetry()
        .UsePrometheusEndpoint();
});

var app = builder.Build();

app.UseMonica();

app.MapGet("/", () => Results.Ok(new
{
    application = "MonicaStarter",
    message = "Monica is running.",
    endpoints = new
    {
        readiness = "/health",
        liveness = "/alive",
        metrics = "/metrics"
    }
}));

app.MapMonica();
app.Run();
