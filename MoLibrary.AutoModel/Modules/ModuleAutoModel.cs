using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.AutoModel.AutoModel.Implements;
using MoLibrary.AutoModel.Implements;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AutoModel.Modules;

public class ModuleAutoModel(ModuleAutoModelOption option) : MoModule<ModuleAutoModel, ModuleAutoModelOption>(option)
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

    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "AutoModel相关接口" }
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
        return Res.Ok();
    }
}