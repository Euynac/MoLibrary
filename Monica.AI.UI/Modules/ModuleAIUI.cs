using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Monica.AI.Chat.Abstractions;
using Monica.AI.UI.Localization;
using Monica.AI.UI.Pages;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Providers.Browser;
using Monica.AI.UI.UIChat.State;
using Monica.AI.UI.UIChat.Support;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// AI UI module registration extension method
/// </summary>
public static class ModuleAIUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure AI UI module
        /// </summary>
        public ModuleRegistration<ModuleAIUI, ModuleAIUIOption> AddAIUI(
            Action<ModuleAIUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleAIUI, ModuleAIUIOption>(action);
            registration.Require<ModuleKnowledgeBase, ModuleKnowledgeBaseOption>();
            registration.Require<ModuleSkillSystem, ModuleSkillSystemOption>();
            registration.Require<ModuleMcp, ModuleMcpOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<AIResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>(option => option.EnableMarkdown = true)
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterLocalizedPage<ChatPage, AIResource>(
                        ChatPage.PAGE_URL,
                        "Pages:AIChat:Title",
                        Icons.Material.Filled.SmartToy,
                        BuiltInNavigationCategoryIds.AI,
                        addToNav: true,
                        navOrder: 1);
                    registry.RegisterLocalizedPage<ProviderManagePage, AIResource>(
                        ProviderManagePage.PAGE_URL,
                        "Pages:AIProviderManage:Title",
                        Icons.Material.Filled.Hub,
                        BuiltInNavigationCategoryIds.AI,
                        addToNav: true,
                        navOrder: 2);
                    registry.RegisterLocalizedPage<AgentCapabilityManagePage, AIResource>(
                        AgentCapabilityManagePage.PAGE_URL,
                        "Pages:AICapabilities:Title",
                        Icons.Material.Filled.Extension,
                        BuiltInNavigationCategoryIds.AI,
                        addToNav: true,
                        navOrder: 3);
                });
            return registration;
        }
    }

    extension(ModuleRegistration<ModuleAIUI, ModuleAIUIOption> registration)
    {
        /// <summary>
        /// Enables durable chat history in the current browser profile.
        /// </summary>
        /// <param name="configure">Optional browser retention configuration.</param>
        /// <returns>The same host-bound registration.</returns>
        public ModuleRegistration<ModuleAIUI, ModuleAIUIOption> UseBrowserChatHistory(
            Action<BrowserChatHistoryOptions>? configure = null)
        {
            var options = new BrowserChatHistoryOptions();
            configure?.Invoke(options);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxSessions);

            return registration.ConfigureServices(context =>
            {
                context.Services.RemoveAll<IChatHistoryProvider>();
                context.Services.RemoveAll<IChatHistoryPartitionResolver>();
                context.Services.AddScoped<IBrowserChatHistoryLock, BrowserChatHistoryWebLock>();
                context.Services.AddScoped<IChatHistoryProvider, BrowserChatHistoryProvider>();
                context.Services.AddScoped<IChatHistoryPartitionResolver, BrowserChatHistoryPartitionResolver>();
                context.Services.AddSingleton<IOptions<BrowserChatHistoryOptions>>(Options.Create(options));
            });
        }
    }
}

/// <summary>
/// AI UI module implementation
/// Provides an AI chat interface based on Blazor
/// </summary>
public class ModuleAIUI : MonicaModule<ModuleAIUIOption>, IUIModule
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleAI, ModuleAIOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleAIUIOption> context)
    {
        // Register UI services
        context.Services.AddScoped<ChatPageState>();
        context.Services.AddScoped<ChatSessionWorkspace>();
    }
}

/// <summary>
/// AI UI module configuration options
/// </summary>
public class ModuleAIUIOption : ModuleOptions<ModuleAIUI>
{
    /// <summary>
    /// Enable Markdown rendering
    /// </summary>
    public bool EnableMarkdown { get; set; } = true;

    /// <summary>
    /// Show provider selector
    /// </summary>
    public bool ShowProviderSelector { get; set; } = true;

    /// <summary>
    /// Show session list
    /// </summary>
    public bool ShowSessionList { get; set; } = true;

    /// <summary>
    /// Default system prompt (overrides backend setting)
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// Maximum width for message bubbles
    /// </summary>
    public string MessageMaxWidth { get; set; } = "80%";

    /// <summary>
    /// Request timeout in milliseconds (0 = no timeout)
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Show connection status indicator
    /// </summary>
    public bool ShowConnectionStatus { get; set; }

    /// <summary>
    /// Enable auto-scroll to bottom on new messages
    /// </summary>
    public bool EnableAutoScroll { get; set; } = true;

    /// <summary>
    /// Default knowledge base IDs to pre-select in the chat UI.
    /// </summary>
    public List<string> DefaultKnowledgeBaseIds { get; set; } = [];

    /// <summary>
    /// Whether to show the knowledge base selector in the chat UI.
    /// </summary>
    public bool ShowKnowledgeBaseSelector { get; set; } = true;
}
