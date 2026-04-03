using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.Core.ObjectMapping.Providers.Mapster;
using Monica.Core.ObjectMapping.Services;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleObjectMappingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the object mapping module.
        /// </summary>
        public static ModuleObjectMappingGuide AddObjectMapping(Action<ModuleObjectMappingOption>? action = null)
        {
            return new ModuleObjectMappingGuide().Register(action);
        }
    }
}

/// <summary>
/// Provides the Mapster-based object mapping capability.
/// </summary>
[ModuleKey(EMoModuleKey.ObjectMapping)]
public class ModuleObjectMapping(ModuleObjectMappingOption option) : MoModule<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (option.DebugMapper)
        {
            //https://github.com/MapsterMapper/Mapster/wiki/Debugging
            TypeAdapterConfig.GlobalSettings.Compiler = exp => ((LambdaExpression)exp).CompileWithDebugInfo(
                new ExpressionCompilationOptions()
                {
                    //ThrowOnFailedCompilation = true,
                    EmitFile = true,
                    References = [Assembly.GetAssembly(typeof(Res))!, Assembly.GetAssembly(typeof(Enumerable))!, .. option.DebuggerRelatedAssemblies ?? []]
                });
        }

        Task.Factory.StartNew(() =>
        {
            TypeAdapterConfig.GlobalSettings.Compile();
        }).ContinueWith((t) =>
        {
            Environment.FailFast($"Mapper编译失败，定义有误，请检查。{t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddTransient<IObjectMapper, MapsterObjectMapper>();
        services.AddScoped<ObjectMappingStatusService>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet("/mapper/status", async (HttpContext context, ObjectMappingStatusService statusService) =>
            {
                var result = await statusService.GetStatusAsync();
                if (result.IsFailed(out var error, out var data))
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { error });
                    return;
                }

                var res = new
                {
                    count = data.Count,
                    cards = data.Mappings.Select(x => new
                    {
                        x.SourceType,
                        x.DestinationType,
                        x.MapExpression
                    })
                };
                await context.Response.WriteAsJsonAsync(res);
            })
            .WithName("获取Mapper状态信息")
            .WithTags(tagName)
            .WithSummary("获取Mapper状态信息")
            .WithDescription("获取Mapper状态信息");
        });
    }
}

public class ModuleObjectMappingGuide : MoModuleGuide<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>
{
}

public class ModuleObjectMappingOption : MoModuleOptionWithMinimalApi<ModuleObjectMapping>
{
    /// <summary>
    /// Enables generation of debuggable Mapster mapping assemblies for manual troubleshooting.
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// Additional assemblies that contain base types or extension methods required when debugging mapping definitions.
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }
}
