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
using Monica.Tool.MoResponse;

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
            catalog.AddModels(EAIProviderType.OpenAI, ModuleAIOption.GetReservedModels(EAIProviderType.OpenAI));
            catalog.AddModels(EAIProviderType.Anthropic, ModuleAIOption.GetReservedModels(EAIProviderType.Anthropic));

            foreach (var registration in Option.ModelRegistrations)
            {
                catalog.AddModel(registration.ProviderType, registration.Model);
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
    /// 添加模型信息
    /// </summary>
    /// <param name="providerType">Provider 类型</param>
    /// <param name="model">模型信息</param>
    /// <returns>当前引导器实例</returns>
    public ModuleAIGuide AddModel(EAIProviderType providerType, AIModelInfo model)
    {
        ConfigureModuleOption(option => option.AddModel(providerType, model));
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

            // 创建会话
            endpoints.MapPost($"{routePrefix}/sessions", (CreateSessionRequest? request) =>
            {
                var session = chatService.CreateSession(
                    request?.ProviderId,
                    request?.Title,
                    request?.SystemPrompt);
                return TypedResults.Ok(new { session.SessionId, session.Title, session.ProviderId });
            });

            // 获取所有会话
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
                    MessageCount = s.Messages.Count
                }));
            });

            // 获取会话详情
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
                    session.Messages
                });
            });

            // 删除会话
            endpoints.MapDelete($"{routePrefix}/sessions/{{sessionId}}", (string sessionId) =>
            {
                var deleted = chatService.DeleteSession(sessionId);
                return deleted ? Results.NoContent() : Results.NotFound();
            });

            // 非流式聊天
            endpoints.MapPost($"{routePrefix}/chat", async (AIChatRequest request, CancellationToken ct) =>
            {
                var result = await chatService.SendMessageAsync(request, ct);
                if (result.IsFailed(out var error, out var data))
                {
                    return Results.BadRequest(error.Message);
                }
                return Results.Ok(data);
            });

            // 流式聊天 (SSE)
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
    internal List<AIModelRegistration> ModelRegistrations { get; } = [];

    internal static IReadOnlyList<AIModelInfo> GetReservedModels(EAIProviderType providerType)
    {
        return providerType switch
        {
            EAIProviderType.OpenAI => OpenAIReservedModels.Models,
            EAIProviderType.Anthropic => AnthropicReservedModels.Models,
            _ => Array.Empty<AIModelInfo>()
        };
    }

    /// <summary>
    /// 添加模型信息
    /// </summary>
    public void AddModel(EAIProviderType providerType, AIModelInfo model)
    {
        ModelRegistrations.Add(new AIModelRegistration(providerType, model));
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

internal record AIModelRegistration(EAIProviderType ProviderType, AIModelInfo Model);
