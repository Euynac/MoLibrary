using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.AutoModel.AutoModel.Implements;
using MoLibrary.AutoModel.Implements;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Tool.Extensions;
using MoLibrary.AutoModel.Modules;

namespace MoLibrary.AutoModel;

/// <summary>
/// ASP.NET Core AutoModel扩展
/// </summary>
public static class AutoModelBuilderExtensions
{
    /// <summary>
    /// 添加AutoModel服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="modelSetting"></param>
    /// <param name="normalizerSetting"></param>
    /// <returns></returns>
    public static IServiceCollection AddAutoModel(this IServiceCollection services, Action<ModuleAutoModelOption>? modelSetting = null, Action<AutoModelExpressionOptions>? normalizerSetting = null)
    {
        //add options using IOption pattern.
        if (modelSetting != null)
        {
            //services.AddOptions<AutoModelOptions>().Configure(modelSetting);
            services.Configure(modelSetting);
        }

        if (normalizerSetting != null)
        {
            services.Configure(normalizerSetting);
        }

        services.AddSingleton<IAutoModelSnapshotFactory, AutoModelSnapshotFactoryMemoryProvider>();
        services.AddSingleton(typeof(IAutoModelSnapshot<>), typeof(AutoModelSnapshotMemoryProvider<>));
        services.AddTransient(typeof(IAutoModelExpressionNormalizer<>), typeof(AutoModelExpressionNormalizerDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelDbOperator<>), typeof(AutoModelDbOperatorDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelMemoryOperator<>), typeof(AutoModelMemoryOperatorDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelExpressionTokenizer<>),
            typeof(AutoModelExpressionTokenizer<>));
        services.AddTransient<IAutoModelTokenExpressionGen, AutoModelTokenExpressionGenDynamicLinqProvider>();
        services.AddTransient<IAutoModelTypeConverter, AutoModelTypeConverter>();

        return services;
    }

    /// <summary>
    /// 使用AutoModel中间件(HTTP Endpoints等)
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsAutoModel(this IApplicationBuilder app, string? groupName = "AutoModel")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "AutoModel相关接口" }
            };
            endpoints.MapGet("/auto-model/status", async (HttpResponse response, HttpContext context, [FromQuery] string? specificEntity = null) =>
            {
                var factory = app.ApplicationServices.GetRequiredService<IAutoModelSnapshotFactory>();
                var snapshots = factory.GetSnapshots().WhereIf(specificEntity != null,
                    p => p.Table.Name?.Equals(specificEntity?.Trim(), StringComparison.OrdinalIgnoreCase) == true).ToList();
                var res = new
                {
                    count = snapshots.Count,
                    entites = snapshots.Select(p => p.Table.Name),
                    snapshots = snapshots.Select(x => new
                    {
                        x.Table,
                        x.Fields
                    })
                };
                await response.WriteAsJsonAsync(res);
            }).WithName("获取AutoModel状态信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取AutoModel状态信息";
                operation.Description = "获取AutoModel状态信息";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}