using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Services;
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
using Monica.Core.Extensions;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

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
    : WebModuleBase<ModuleAI, ModuleAIOption, ModuleAIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleSkillSystemGuide>().Register();
        DependsOnModule<ModuleMcpGuide>().Register();
    }

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
        services.TryAddSingleton<IAgentCapabilityStateStore, FileAgentCapabilityStateStore>();
        services.TryAddSingleton<AIChatRuntimeContextAccessor>();
        services.TryAddSingleton<IAIChatRuntimeContextAccessor>(sp =>
            sp.GetRequiredService<AIChatRuntimeContextAccessor>());
        services.AddSingleton<AIProviderRegistry>();
        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderRegistry>());
        services.AddSingleton<IAIChatAgentFactory, AIChatAgentFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAIChatAgentDecorator, ToolInvocationTrackingAgentDecorator>());

        // Register chat service
        services.AddSingleton<AIChatService>();
        services.AddScoped<ChatFacade>();
        services.AddScoped<ProviderFacade>();
        services.AddScoped<AgentCapabilityFacade>();
    }
}

/// <summary>
/// Builder for AI module configuration.
/// </summary>
public class ModuleAIGuide : WebModuleGuide<ModuleAI, ModuleAIOption, ModuleAIGuide>
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
            RegisterProvider(
                context,
                options,
                nameof(EAIProviderType.OpenAI),
                "OpenAI-compatible chat and embedding provider.",
                "openai",
                supportsRemoteModelListing: true,
                modelCatalog =>
                {
                    EnsureApiKeyConfigured(options);
                    return new OpenAIProvider(options, modelCatalog);
                });
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
            RegisterProvider(
                context,
                options,
                nameof(EAIProviderType.Anthropic),
                "Anthropic Claude chat provider.",
                "anthropic",
                supportsRemoteModelListing: true,
                modelCatalog =>
                {
                    EnsureApiKeyConfigured(options);
                    return new AnthropicProvider(options, modelCatalog);
                });
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

    private static void RegisterProvider<TOptions>(
        ModuleApplicationConfigurationContext<ModuleAIOption> context,
        TOptions options,
        string providerType,
        string description,
        string icon,
        bool supportsRemoteModelListing,
        Func<AIModelCatalog, IAIProvider> providerFactory)
        where TOptions : AIProviderOptions
    {
        var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderRegistry>();
        var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();

        try
        {
            manager.RegisterProvider(providerFactory(modelCatalog));
        }
        catch (Exception ex)
        {
            manager.RegisterProvider(DisabledAIProvider.FromOptions(
                options,
                modelCatalog,
                providerType,
                description,
                icon,
                BuildConfigurationErrors(options, ex),
                supportsRemoteModelListing));
        }
    }

    private static void EnsureApiKeyConfigured(AIProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "API key is empty. Configure a non-empty API key to enable this provider.");
        }
    }

    private static IReadOnlyList<string> BuildConfigurationErrors(AIProviderOptions options, Exception ex)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add("API key is empty. Configure a non-empty API key to enable this provider.");
        }

        var exceptionMessage = ex.GetMessageRecursively();
        if (errors.All(existing => !string.Equals(existing, exceptionMessage, StringComparison.Ordinal)))
        {
            errors.Add($"Provider initialization failed: {exceptionMessage}");
        }

        return errors;
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
                TypedResults.Ok(providerFactory.GetAllProviderInfos()))
                .WithMetadata(MonicaMinimalApiMetadata.Instance);

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

    /// <summary>
    /// Relative or absolute file path used to persist runtime Skill and MCP enablement state.
    /// Defaults to <c>monica_data/ai/capabilities_state.json</c>. Configure this when multiple hosts should
    /// isolate capability-management state or when the default runtime data directory is unsuitable.
    /// </summary>
    public string CapabilityStateStoreFilePath { get; set; } = "monica_data/ai/capabilities_state.json";
}
