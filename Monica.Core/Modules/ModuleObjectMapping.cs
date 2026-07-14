using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.Core.ObjectMapping.Facades;
using Monica.Core.ObjectMapping.Providers.Mapster;
using Monica.Core.ObjectMapping.Services;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleObjectMappingBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the object mapping module.
        /// </summary>
        public ModuleObjectMappingGuide AddObjectMapping(Action<ModuleObjectMappingOption>? action = null)
        {
            return builder.AddModule<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>(action);
        }
    }
}

/// <summary>
/// Provides the Mapster-based object mapping capability.
/// </summary>
[ModuleKey(BuiltInModuleKey.ObjectMapping)]
public class ModuleObjectMapping(ModuleObjectMappingOption option) : WebModuleBase<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var mapsterConfig = new TypeAdapterConfig();

        if (option.DebugMapper)
        {
            mapsterConfig.Compiler = expression => ((LambdaExpression)expression).CompileWithDebugInfo(
                new ExpressionCompilationOptions()
                {
                    EmitFile = true,
                    References = [Assembly.GetAssembly(typeof(Res))!, Assembly.GetAssembly(typeof(Enumerable))!, .. option.DebuggerRelatedAssemblies ?? []]
                });
        }

        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddTransient<IObjectMapper, MapsterObjectMapper>();
        services.AddSingleton<MapsterMappingInspector>();
        services.AddScoped<ObjectMappingStatusService>();
        services.AddScoped<ObjectMappingFacade>(serviceProvider => new ObjectMappingFacade(
            serviceProvider.GetRequiredService<ObjectMappingStatusService>(),
            serviceProvider.GetRequiredService<ILogger<ObjectMappingFacade>>()));
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet("/mapper/status", async (HttpContext context, ObjectMappingFacade facade) =>
            {
                var result = await facade.GetStatusAsync();
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
            .WithName("GetObjectMappingStatus")
            .WithTags(tagName)
            .WithSummary("Gets object mapping status")
            .WithDescription("Returns the mapping pairs and generated Mapster expressions owned by this Monica host.");
        });
    }
}

/// <summary>
/// Provides fluent configuration for the object mapping module.
/// </summary>
public class ModuleObjectMappingGuide : WebModuleGuide<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>
{
}

/// <summary>
/// Configures the Mapster object mapping runtime for one Monica host.
/// </summary>
public class ModuleObjectMappingOption : MinimalApiModuleOptions<ModuleObjectMapping>
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
