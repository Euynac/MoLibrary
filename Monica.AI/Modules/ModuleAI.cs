using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.Providers.Anthropic;
using Monica.AI.Providers.Fake;
using Monica.AI.Providers.OpenAI;
using Monica.AI.Services;
using Monica.AI.Tools;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// AI module builder extension methods
/// </summary>
public static class ModuleAIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure AI module
        /// </summary>
        /// <param name="action">Module configuration options</param>
        /// <returns>AI module configuration guide</returns>
        public static ModuleAIGuide AddAI(Action<ModuleAIOption>? action = null)
        {
            return new ModuleAIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI module
/// </summary>
[ModuleKey(EMoModuleKey.AI)]
public class ModuleAI(ModuleAIOption option)
    : MoModule<ModuleAI, ModuleAIOption, ModuleAIGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register model directory
        services.AddSingleton(sp =>
        {
            var catalog = new AIModelCatalog();
            catalog.AddModels(ModuleAIOption.GetReservedModels());

            foreach (var model in Option.ModelRegistrations)
            {
                catalog.AddModel(model);
            }

            return catalog;
        });

        // Register Provider Manager
        services.TryAddSingleton<ITokenCountProvider, EstimatedUtf8TokenCountProvider>();
        services.AddSingleton<AIProviderManager>();
        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderManager>());
        services.AddSingleton<IAIChatAgentFactory, AIChatAgentFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAIChatAgentDecorator, ToolInvocationTrackingAgentDecorator>());

        // Sign up for chat service
        services.AddSingleton<AIChatService>();
    }
}

/// <summary>
/// AI module configuration guide
/// </summary>
public class ModuleAIGuide : MoModuleGuide<ModuleAI, ModuleAIOption, ModuleAIGuide>
{
    /// <summary>
    /// Add OpenAI Provider
    /// </summary>
    /// <param name="configure">Configure delegation</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>Current bootloader instance</returns>
    public ModuleAIGuide AddOpenAIProvider(
        Action<OpenAIProviderOptions> configure,
        string? providerId = null)
    {
        var options = new OpenAIProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);
        options.ProviderId = providerId ?? options.ProviderId ?? nameof(EAIProviderType.OpenAI);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new OpenAIProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Add Anthropic Provider
    /// </summary>
    /// <param name="configure">Configure delegation</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>Current bootloader instance</returns>
    public ModuleAIGuide AddAnthropicProvider(
        Action<AnthropicProviderOptions> configure,
        string? providerId = null)
    {
        var options = new AnthropicProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);
       
        options.ProviderId = providerId ?? options.ProviderId ?? nameof(EAIProviderType.Anthropic);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new AnthropicProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Add fake embedding provider.
    /// </summary>
    /// <param name="configure">Options configure delegate.</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>Current guide instance.</returns>
    public ModuleAIGuide AddFakeProvider(
        Action<FakeProviderOptions> configure,
        string? providerId = null)
    {
        var options = new FakeProviderOptions { ApiKey = "fake", SupportedModels = [] };
        configure(options);
        options.ProviderId = providerId ?? options.ProviderId ?? nameof(EAIProviderType.Fake);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new FakeProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Add model information to the catalog
    /// </summary>
    /// <param name="model">Model information</param>
    /// <returns>Current guide instance</returns>
    public ModuleAIGuide AddModel(AIModelInfo model)
    {
        ConfigureModuleOption(option => option.AddModel(model), secondKey: model.ModelName);
        return this;
    }

    /// <summary>
    /// Add custom provider
    /// </summary>
    /// <typeparam name="TProvider">Provider type</typeparam>
    /// <param name="providerFactory">Provider factory method</param>
    /// <returns>Current bootloader instance</returns>
    public ModuleAIGuide AddProvider<TProvider>(Func<IServiceProvider, TProvider> providerFactory)
        where TProvider : class, IAIProvider
    {
        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var provider = providerFactory(context.ApplicationBuilder.ApplicationServices);
            manager.RegisterProvider(provider);
        }, secondKey: $"custom-{typeof(TProvider).Name}", order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Map AI chat endpoints
    /// </summary>
    /// <param name="routePrefix">Route prefix, defaults to "/ai"</param>
    /// <returns>Current guide instance</returns>
    public ModuleAIGuide MapAIEndpoints(string routePrefix = "/ai")
    {
        ConfigureEndpoints(builder =>
        {
            var endpoints = builder.RequireWebApplication();
            var providerFactory = endpoints.Services.GetRequiredService<IAIProviderFactory>();

            // Get all providers
            endpoints.MapGet($"{routePrefix}/providers", () =>
                TypedResults.Ok(providerFactory.GetAllProviderInfos()));

            // Note: Session management endpoints removed as sessions are now managed by UI layer.
            // API endpoints should be stateless and not manage sessions.
            // For stateful chat, use the UI service layer (AIChatUIService).
        });

        return this;
    }
}

/// <summary>
/// AI module options
/// </summary>
public class ModuleAIOption : MoModuleOption<ModuleAI>
{
    internal List<AIModelInfo> ModelRegistrations { get; } = [];

    internal static IReadOnlyList<AIModelInfo> GetReservedModels()
    {
        return [..OpenAIReservedModels.Models, ..AnthropicReservedModels.Models];
    }

    /// <summary>
    /// Add model information
    /// </summary>
    public void AddModel(AIModelInfo model)
    {
        ModelRegistrations.Add(model);
    }

    /// <summary>
    /// Default system prompt word
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// Default maximum number of context messages (0 means no limit)
    /// </summary>
    public int MaxContextMessages { get; set; }

    /// <summary>
    /// Whether to enable request logging
    /// </summary>
    public bool EnableRequestLogging { get; set; }
}
