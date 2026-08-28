using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Monica.Modules;
using Monica.SignalR.Facades;
using Monica.SignalR.Localization;
using Monica.SignalR.UISignalR.Support;

namespace Monica.SignalR.UISignalR.State;

/// <summary>
/// Creates page-owned SignalR debug state and its isolated browser session.
/// </summary>
public sealed class SignalRDebugPageStateFactory(
    IOptions<ModuleSignalRUIOption> options,
    SignalRFacade signalRFacade,
    IJSRuntime jsRuntime,
    SignalRInvocationArgumentParser argumentParser,
    SignalRDebugTestTokenService testTokenService,
    IStringLocalizer<SignalRResource> localizer)
{
    /// <summary>
    /// Creates a fresh state tree for one rendered debug page.
    /// </summary>
    public SignalRDebugPageState Create()
    {
        var jsClient = new SignalRDebugJsClient(jsRuntime, argumentParser, localizer);
        return new SignalRDebugPageState(options, signalRFacade, jsClient, testTokenService);
    }
}
