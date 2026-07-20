using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DevOps.Terminal.Facades;
using Monica.DevOps.Terminal.Services;
using Monica.DevOps.Terminal.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the local terminal module.
/// </summary>
public static class ModuleTerminalBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the local terminal module and applies optional configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The terminal module guide.</returns>
        public ModuleTerminalGuide AddTerminal(Action<ModuleTerminalOption>? action = null)
        {
            return builder.AddModule<ModuleTerminal, ModuleTerminalOption, ModuleTerminalGuide>(action);
        }
    }
}

/// <summary>
/// Local process-backed terminal module.
/// </summary>
/// <param name="option">The terminal module options.</param>
[ModuleKey(BuiltInModuleKey.Terminal)]
public sealed class ModuleTerminal(ModuleTerminalOption option)
    : ModuleBase<ModuleTerminal, ModuleTerminalOption, ModuleTerminalGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TerminalEnvironmentDetector>();
        services.AddSingleton<TerminalCommandRunner>();
        services.AddSingleton<TerminalSessionService>();
        services.AddScoped<TerminalFacade>();
    }
}

/// <summary>
/// Fluent guide for the local terminal module.
/// </summary>
public sealed class ModuleTerminalGuide : ModuleGuide<ModuleTerminal, ModuleTerminalOption, ModuleTerminalGuide>
{
}

/// <summary>
/// Configuration options for the local terminal module.
/// </summary>
public sealed class ModuleTerminalOption : ModuleOptions<ModuleTerminal>
{
    /// <summary>
    /// Gets or sets the default command timeout in seconds. UI calls use this value when no override is supplied.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the maximum timeout in seconds accepted by the command runner.
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 900;

    /// <summary>
    /// Gets or sets the maximum number of command output lines retained per command result.
    /// </summary>
    public int MaxOutputLines { get; set; } = 2_000;

    /// <summary>
    /// Gets or sets the maximum number of history entries retained per terminal session.
    /// </summary>
    public int MaxHistoryEntries { get; set; } = 100;
}
