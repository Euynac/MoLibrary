using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.AI.Abstractions;
using Monica.AI.Extensions;
using Monica.AI.Models;
using Monica.AI.Providers;
using Monica.AI.Providers.Anthropic;
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
    /// <returns>当前引导器实例</returns>
    public ModuleAIGuide AddOpenAIProvider(Action<OpenAIProviderOptions> configure)
    {
        var options = new OpenAIProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new OpenAIProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: $"openai-{options.ProviderId ?? options.SupportedModels?.FirstOrDefault() ?? "invalid"}", order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// 添加 Anthropic Provider
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>当前引导器实例</returns>
    public ModuleAIGuide AddAnthropicProvider(Action<AnthropicProviderOptions> configure)
    {
        var options = new AnthropicProviderOptions { ApiKey = "", SupportedModels = [] };
        configure(options);

        ConfigureApplicationBuilder(context =>
        {
            var manager = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIProviderManager>();
            var modelCatalog = context.ApplicationBuilder.ApplicationServices.GetRequiredService<AIModelCatalog>();
            var provider = new AnthropicProvider(options, modelCatalog);
            manager.RegisterProvider(provider);
        }, secondKey: $"anthropic-{options.ProviderId ?? options.SupportedModels?.FirstOrDefault() ?? "invalid"}", order: EMoModuleApplicationMiddlewaresOrder.BeforeUseRouting);

        return this;
    }

    /// <summary>
    /// Add model information to the catalog
    /// </summary>
    /// <param name="model">Model information</param>
    /// <returns>Current guide instance</returns>
    public ModuleAIGuide AddModel(AIModelInfo model)
    {
        ConfigureModuleOption(option => option.AddModel(model));
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
    /// 映射 AI 聊天端点
    /// </summary>
    /// <param name="routePrefix">路由前缀，默认为 "/ai"</param>
    /// <returns>当前引导器实例</returns>
    public ModuleAIGuide MapAIEndpoints(string routePrefix = "/ai")
    {
        ConfigureEndpoints(builder =>
        {
            var endpoints = builder.WebApplication;
            var chatService = endpoints.Services.GetRequiredService<AIChatService>();
            var providerFactory = endpoints.Services.GetRequiredService<IAIProviderFactory>();

            // 获取所有 Provider
            endpoints.MapGet($"{routePrefix}/providers", () =>
                TypedResults.Ok(providerFactory.GetAllProviderInfos()));

            // Create session
            endpoints.MapPost($"{routePrefix}/sessions", async (CreateSessionRequest? request, CancellationToken ct) =>
            {
                var session = await chatService.CreateSessionAsync(
                    request?.ProviderId,
                    request?.Title,
                    request?.SystemPrompt,
                    knowledgeBaseIds: null,
                    ct);
                return TypedResults.Ok(new { session.SessionId, session.Title, session.ProviderId });
            });

            // Get all sessions
            endpoints.MapGet($"{routePrefix}/sessions", () =>
            {
                var sessions = chatService.GetAllSessions();
                return TypedResults.Ok(sessions.Select(s => new
                {
                    s.SessionId,
                    s.Title,
                    s.ProviderId,
                    s.CreatedAt,
                    s.UpdatedAt,
                    s.MessageCount
                }));
            });

            // Get session details
            endpoints.MapGet($"{routePrefix}/sessions/{{sessionId}}", (string sessionId) =>
            {
                var session = chatService.GetSession(sessionId);
                if (session == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(new
                {
                    session.SessionId,
                    session.Title,
                    session.ProviderId,
                    session.ModelName,
                    session.SystemPrompt,
                    session.CreatedAt,
                    session.UpdatedAt,
                    session.MessageCount
                });
            });

            // Delete session
            endpoints.MapDelete($"{routePrefix}/sessions/{{sessionId}}", (string sessionId) =>
            {
                var deleted = chatService.DeleteSession(sessionId);
                return deleted ? Results.NoContent() : Results.NotFound();
            });

            // Non-streaming chat
            endpoints.MapPost($"{routePrefix}/chat", async (AIChatRequest request, CancellationToken ct) =>
            {
                try
                {
                    var response = await chatService.SendMessageAsync(request, ct);
                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            // Streaming chat (SSE)
            endpoints.MapPost($"{routePrefix}/chat/stream", (AIChatRequest request, CancellationToken ct) =>
            {
                var stream = chatService.SendMessageStreamingAsync(request, ct);
                return TypedResults.ServerSentEvents(stream.ToSseItems(ct));
            });
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
