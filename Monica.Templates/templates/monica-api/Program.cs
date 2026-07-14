using Monica.Core.Modularity.Extensions;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

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
        health = "/healthz",
        metrics = "/metrics"
    }
}));

app.MapHealthChecks("/healthz");

app.MapMonica();
app.Run();
