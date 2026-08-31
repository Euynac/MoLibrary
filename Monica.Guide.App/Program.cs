using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Monica.Guide;
using Monica.Guide.App;
using Monica.Guide.App.Components;

var options = GuideAppOptions.Parse(args);

// One executable, two faces: a known guide subcommand dispatches the CLI; anything else
// launches the token-gated setup wizard for the selected (or detected) product.
if (options.CliArguments is { } cliArguments)
{
    return await GuideCommandRunner.RunAsync(
        options.Product ?? KnownAgentProducts.Monica,
        cliArguments,
        Console.Out,
        Console.Error,
        currentVersion: () => GuideAppInfo.CurrentVersion);
}

var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
var product = options.Product ?? KnownAgentProducts.Monica;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory
});
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
// IDE debugging and direct Debug-executable launches run from build output, where the
// default Production static-assets gate would serve the Blazor framework scripts as
// empty bodies and the interactive circuit would never start. Publish layouts already
// carry the real files, so manifest-based serving is only forced when they are absent.
if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "_framework", "blazor.web.js")))
{
    builder.WebHost.UseStaticWebAssets();
}
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// An explicit --bundle path pre-seeds the wizard and wins over structural defaults.
// SwitchProduct seeds the port default for serving products; the install page refreshes
// it from the persisted server configuration once the dashboard loads.
var session = new SetupSession { BundlePath = options.BundlePath ?? string.Empty };
session.SwitchProduct(product);
builder.Services.AddSingleton(session);
builder.Services.AddSingleton<SetupFacade>();

var app = builder.Build();

// The setup UI mutates host configuration; only the loopback URL carrying the
// per-run token may reach it, so every other path is refused before routing.
// Routing is registered explicitly so the prefix strip runs before route matching,
// and PathBase carries the token into <base href> so browsers resolve scripts and
// relative links inside the tokened scope.
app.Use(async (context, next) =>
{
    var prefix = $"/s/{token}";
    if (!context.Request.Path.StartsWithSegments(prefix, out var remaining))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Request.PathBase = prefix;
    context.Request.Path = remaining;
    await next();
});
app.UseRouting();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.StartAsync();

// The one host-and-runtime diagnosis of this run starts immediately in the background:
// agent-host detection shells out to agent CLIs and takes seconds, and the Overview must
// paint instantly from the cache instead of waiting for it on every visit.
app.Services.GetRequiredService<SetupFacade>().StartDashboardWarmup();

var address = app.Urls.FirstOrDefault(static url => url.StartsWith("http://127.0.0.1", StringComparison.Ordinal))
              ?? $"http://127.0.0.1:{options.Port}";
var setupUrl = $"{address}/s/{token}/";
Console.WriteLine($"Monica Guide {GuideAppInfo.CurrentVersion} — {product.DisplayName}");
Console.WriteLine(setupUrl);

if (options.OpenBrowser)
{
    try
    {
        Process.Start(new ProcessStartInfo(setupUrl) { UseShellExecute = true });
    }
    catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
    {
        Console.Error.WriteLine($"Could not open the browser automatically: {exception.Message}");
    }
}

await app.WaitForShutdownAsync();
return 0;
