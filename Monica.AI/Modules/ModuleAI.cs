using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Abstractions;
using Monica.AI.Extensions;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.Providers.Anthropic;
using Monica.AI.Providers.Fake;
using Monica.AI.Providers.OpenAI;
using Monica.AI.Services;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.AI.Modules;

/// <summary>
/// AI 模块构建器扩展方法
/// </summary>
public static class ModuleAIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 AI 模块
        /// </summary>
        /// <param name="action">模块配置选项</param>
        /// <returns>AI 模块配置引导器</returns>
        public static ModuleAIGuide AddAI(Action<ModuleAIOption>? action = null)
        {
            return new ModuleAIGuide().Register(action);
        }
    }
}

/// <summary>
/// AI 模块
/// </summary>
public class ModuleAI(ModuleAIOption option)
    : MoModule<ModuleAI, ModuleAIOption, ModuleAIGuide>(option)
{
    /// <inheritdoc />
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.AI;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册模型目录
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

        // 注册 Provider 管理器
        services.AddSingleton<AIProviderManager>();
        services.AddSingleton<IAIProviderFactory>(sp => sp.GetRequiredService<AIProviderManager>());

        // 注册聊天服务
        services.AddSingleton<AIChatService>();
    }
}

/// <summary>
/// AI 模块配置引导器
/// </summary>
public class ModuleAIGuide : MoModuleGuide<ModuleAI, ModuleAIOption, ModuleAIGuide>
{
    /// <summary>
    /// 添加 OpenAI Provider
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>当前引导器实例</returns>
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
    /// 添加 Anthropic Provider
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <param name="providerId">Optional provider identifier. Defaults to provider type.</param>
    /// <returns>当前引导器实例</returns>
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
    /// 添加自定义 Provider
    /// </summary>
    /// <typeparam name="TProvider">Provider 类型</typeparam>
    /// <param name="providerFactory">Provider 工厂方法</param>
    /// <returns>当前引导器实例</returns>
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
            var endpoints = builder.WebApplication;
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
/// AI 模块选项
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
    /// 默认系统提示词
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// 默认最大上下文消息数量（0 表示不限制）
    /// </summary>
    public int MaxContextMessages { get; set; }

    /// <summary>
    /// 是否启用请求日志
    /// </summary>
    public bool EnableRequestLogging { get; set; }
}
