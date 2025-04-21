using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.Tool.MoResponse;
using MoLibrary.AutoModel.AutoModel.Implements;
using MoLibrary.AutoModel.Implements;
using MoLibrary.AutoModel.Interfaces;

namespace MoLibrary.AutoModel;

public static class ModuleBuilderExtensionsAuthorization
{
    public static ModuleGuideAutoModel AddMoModuleAutoModel(this IServiceCollection services, Action<ModuleOptionAutoModel>? action = null)
    {
        return new ModuleGuideAutoModel().Register(action);
    }
}

public class ModuleAutoModel(ModuleOptionAutoModel option) : MoModule<ModuleAutoModel, ModuleOptionAutoModel>(option)
{
    public override Res ConfigureServices(IServiceCollection services)
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
        return Res.Ok();
    }

    public override EMoModules CurModuleEnum()
    {
        return EMoModules.AutoModel;
    }

    public override Res UseMiddlewares(IApplicationBuilder app)
    {
        return Res.Ok();
        //app.UseEndpoints(endpoints =>
        //{
        //    var tagGroup = new List<OpenApiTag>
        //    {
        //        new() { Name = groupName, Description = "AutoModel相关接口" }
        //    };
        //    endpoints.MapGet("/auto-model/status", async (HttpResponse response, HttpContext context, [FromQuery] string? specificEntity = null) =>
        //    {
        //        var factory = app.ApplicationServices.GetRequiredService<IAutoModelSnapshotFactory>();
        //        var snapshots = factory.GetSnapshots().WhereIf(specificEntity != null,
        //            p => p.Table.Name?.Equals(specificEntity?.Trim(), StringComparison.OrdinalIgnoreCase) == true).ToList();
        //        var res = new
        //        {
        //            count = snapshots.Count,
        //            entites = snapshots.Select(p => p.Table.Name),
        //            snapshots = snapshots.Select(x => new
        //            {
        //                x.Table,
        //                x.Fields
        //            })
        //        };
        //        await response.WriteAsJsonAsync(res);
        //    }).WithName("获取AutoModel状态信息").WithOpenApi(operation =>
        //    {
        //        operation.Summary = "获取AutoModel状态信息";
        //        operation.Description = "获取AutoModel状态信息";
        //        operation.Tags = tagGroup;
        //        return operation;
        //    });
        //});
    }
}

public class ModuleGuideAutoModel : MoModuleGuide<ModuleAutoModel, ModuleOptionAutoModel, ModuleGuideAutoModel>
{


}
