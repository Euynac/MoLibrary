using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.AutoModel.Implements;
using Monica.AutoModel.Interfaces;
using Monica.Core.ExceptionHandler;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.Tool.Extensions;

namespace Monica.AutoModel.Modules;

public static class ModuleAutoModelBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 AutoModel 模块
        /// </summary>
        public static ModuleAutoModelGuide AddAutoModel(Action<ModuleAutoModelOption>? action = null)
        {
            return new ModuleAutoModelGuide().Register(action);
        }
    }
}

public class ModuleAutoModel(ModuleAutoModelOption option) : MoModuleWithDependencies<ModuleAutoModel, ModuleAutoModelOption, ModuleAutoModelGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAutoModelSnapshotFactory, AutoModelSnapshotFactoryMemoryProvider>();
        services.AddSingleton(typeof(IAutoModelSnapshot<>), typeof(AutoModelSnapshotMemoryProvider<>));
        services.AddTransient(typeof(IAutoModelExpressionNormalizer<>), typeof(AutoModelExpressionNormalizerDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelDbOperator<>), typeof(AutoModelDbOperatorDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelMemoryOperator<>), typeof(AutoModelMemoryOperatorDynamicLinqProvider<>));
        services.AddTransient(typeof(IAutoModelExpressionTokenizer<>),
            typeof(AutoModelExpressionTokenizer<>));
        services.AddTransient<IAutoModelTokenExpressionGen, AutoModelTokenExpressionGenDynamicLinqProvider>();
        services.AddTransient<IAutoModelTypeConverter, AutoModelTypeConverter>();
    }

    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.AutoModel;
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

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
            })
            .WithName("获取AutoModel状态信息")
            .WithTags(tagName)
            .WithSummary("获取AutoModel状态信息")
            .WithDescription("获取AutoModel状态信息");
        });
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableGlobalExceptionHandler)
        {
            DependsOnModule<ModuleGlobalExceptionHandlerGuide>().Register();
        }
    }
}

public class ModuleAutoModelGuide : MoModuleGuide<ModuleAutoModel, ModuleAutoModelOption, ModuleAutoModelGuide>
{


}

public class ModuleAutoModelOption : MoModuleOptionWithMinimalApi<ModuleAutoModel>, IMoModuleOptionUseGlobalException
{
    /// <summary>
    /// 全局主动模式（仅使用了AutoField标签的字段才会启用自动模型功能）
    /// </summary>
    public bool EnableActiveMode { get; set; }

    /// <summary>
    /// 默认激活名开启前缀忽略
    /// </summary>
    public bool EnableIgnorePrefix { get; set; }

    /// <summary>
    /// 默认激活名开启前缀忽略后，自动调整失败的激活名不报错
    /// </summary>
    public bool EnableIgnorePrefixAutoAdjust { get; set; }

    /// <summary>
    /// 开启调试模式（如显示Filter实际生成Expression）
    /// </summary>
    public bool EnableDebugging { get; set; }
    /// <summary>
    /// 开启将字段显示名作为激活名
    /// </summary>
    [Obsolete("暂未实现")]
    public bool EnableTitleAsActivateName { get; set; }

    public bool DisableAutoIgnorePropertyWithJsonIgnoreAttribute { get; set; }
    public bool DisableAutoIgnorePropertyWithNotMappedAttribute { get; set; }

    /// <summary>
    /// 开启对于不支持的字段类型进行异常报错
    /// </summary>
    public bool EnableErrorForUnsupportedFieldTypes { get; set; }

    public bool DisableGlobalExceptionHandler { get; set; }
}
