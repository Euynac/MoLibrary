using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.AI.Abstractions;
using Monica.AI.Facades;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.Providers.Anthropic;
using Monica.AI.Providers.Fake;
using Monica.AI.Providers.OpenAI;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for configuring the AI module builder.
/// </summary>
public static class ModuleAIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the AI module.
        /// </summary>
        /// <param name="action">The module configuration action.</param>
        /// <returns>An AI module configuration builder.</returns>
        public static ModuleAIGuide AddAI(Action<ModuleAIOption>? action = null)
        {
            return new ModuleAIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.AI)]
public class ModuleAI(ModuleAIOption option)
    : ModuleBase<ModuleAI, ModuleAIOption, ModuleAIGuide>(option)
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

        // Register provider manager
        services.TryAddSingleton<ITokenCountProvider, EstimatedUtf8TokenCountProvider>();
        services.AddSingleton<AIProviderRegistry>();
        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderRegistry>());
        services.AddSingleton<IAIChatAgentFactory, AIChatAgentFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAIChatAgentDecorator, ToolInvocationTrackingAgentDecorator>());

        // Register chat service
        services.AddSingleton<AIChatService>();
        services.AddScoped<ChatFacade>();
        services.AddScoped<ProviderFacade>();
    }
}

/// <summary>
/// Builder for AI module configuration.
/// </summary>
public class ModuleAIGuide : ModuleGuide<ModuleAI, ModuleAIOption, ModuleAIGuide>
{
    /// <summary>
    /// Adds an OpenAI provider.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>The current builder instance.</returns>
    public ModuleAIGuide AddOpenAIProvider(
        Action<OpenAIProviderOptions> configure,
        string? providerId = null)
    {
        var options = new OpenAIProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);
        options.ProviderId = providerId ?? options.ProviderId ?? nameof(EAIProviderType.OpenAI);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderRegistry>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new OpenAIProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: ModuleApplicationMiddlewareOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Adds an Anthropic provider.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>The current builder instance.</returns>
    public ModuleAIGuide AddAnthropicProvider(
        Action<AnthropicProviderOptions> configure,
        string? providerId = null)
    {
        var options = new AnthropicProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);
       
        options.ProviderId = providerId ?? options.ProviderId ?? nameof(EAIProviderType.Anthropic);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderRegistry>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new AnthropicProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: ModuleApplicationMiddlewareOrder.BeforeUseRouting);

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
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderRegistry>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new FakeProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: options.ProviderId, order: ModuleApplicationMiddlewareOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Adds model information to the catalog.
    /// </summary>
    /// <param name="model">The model information to add.</param>
    /// <returns>The current builder instance.</returns>
    public ModuleAIGuide AddModel(AIModelInfo model)
    {
        ConfigureModuleOption(option => option.AddModel(model), secondKey: model.ModelName);
        return this;
    }

    /// <summary>
    /// Adds a custom provider.
    /// </summary>
    /// <typeparam name="TProvider">Provider type</typeparam>
    /// <param name="providerFactory">Provider factory method</param>
    /// <returns>The current builder instance.</returns>
    public ModuleAIGuide AddProvider<TProvider>(Func<IServiceProvider, TProvider> providerFactory)
        where TProvider : class, IAIProvider
    {
        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderRegistry>();
            var provider = providerFactory(context.ApplicationBuilder.ApplicationServices);
            manager.RegisterProvider(provider);
        }, secondKey: $"custom-{typeof(TProvider).Name}", order: ModuleApplicationMiddlewareOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Maps AI chat endpoints.
    /// </summary>
    /// <param name="routePrefix">The route prefix. Defaults to <c>"/ai"</c>.</param>
    /// <returns>The current builder instance.</returns>
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
            // For stateful chat, use the UI chat coordination layer.
        });

        return this;
    }
}

/// <summary>
/// Options for the AI module.
/// </summary>
public class ModuleAIOption : ModuleOptions<ModuleAI>
{
    internal List<AIModelInfo> ModelRegistrations { get; } = [];

    internal static IReadOnlyList<AIModelInfo> GetReservedModels()
    {
        return [..OpenAIReservedModels.Models, ..AnthropicReservedModels.Models];
    }

    /// <summary>
    /// Adds model information.
    /// </summary>
    public void AddModel(AIModelInfo model)
    {
        ModelRegistrations.Add(model);
    }

    /// <summary>
    /// Default system prompt.
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// Default maximum number of context messages. A value of 0 means no limit.
    /// </summary>
    public int MaxContextMessages { get; set; }

    /// <summary>
    /// Indicates whether request logging is enabled.
    /// </summary>
    public bool EnableRequestLogging { get; set; }
}
