using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.UI.Shell.Support;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Evaluates the secure diagnostics boundary before the page invokes Core or the shell exposes its navigation item.
/// </summary>
public sealed class ModuleSystemWorkbenchAccess(
    IHostEnvironment environment,
    IOptions<ModuleSystemUIOption> options,
    IServiceProvider services)
    : DevelopmentPageAccessPolicy<ModuleSystemUIOption>(environment, options, services);
