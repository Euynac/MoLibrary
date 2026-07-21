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
    /// <inheritdoc />
    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }

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

        foreach (var profileType in option.ProfileTypes)
        {
            var profile = Activator.CreateInstance(profileType, nonPublic: true) as IRegister
                ?? throw new InvalidOperationException(
                    $"Object-mapping profile '{profileType.FullName}' must be a concrete {nameof(IRegister)} type with a parameterless constructor.");
            profile.Register(mapsterConfig);
        }

        mapsterConfig.Compile(failFast: false);

        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddScoped<IObjectMapper, MapsterObjectMapper>();
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
    /// <summary>
    /// Adds a Mapster profile to the current host's object-mapping configuration.
    /// </summary>
    /// <typeparam name="TProfile">
    /// A stateless profile with a parameterless constructor. The profile may be non-public because Monica activates it
    /// only while composing the owning host.
    /// </typeparam>
    /// <returns>The current guide for fluent configuration.</returns>
    /// <remarks>
    /// Profiles execute once in registration order. Register shared platform profiles before domain profiles and
    /// adapter profiles. Repeating the same profile type is idempotent within one host.
    /// </remarks>
    public ModuleObjectMappingGuide AddProfile<TProfile>()
        where TProfile : class, IRegister
    {
        var profileType = typeof(TProfile);
        var profileKey = profileType.AssemblyQualifiedName
            ?? throw new InvalidOperationException(
                $"Object-mapping profile '{profileType.FullName}' does not have an assembly-qualified type name.");

        ConfigureModuleOption(
            moduleOption => moduleOption.AddProfile(profileType, profileKey),
            secondKey: profileKey,
            duplicateBehavior: ModuleConfigurationDuplicateBehavior.SilentIdempotent);
        return this;
    }
}

/// <summary>
/// Configures the Mapster object mapping runtime for one Monica host.
/// </summary>
public class ModuleObjectMappingOption : MinimalApiModuleOptions<ModuleObjectMapping>
{
    private readonly HashSet<string> _profileKeys = new(StringComparer.Ordinal);
    private readonly List<Type> _profileTypes = [];

    /// <summary>
    /// Enables generation of debuggable Mapster mapping assemblies for manual troubleshooting.
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// Additional assemblies that contain base types or extension methods required when debugging mapping definitions.
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }

    /// <summary>
    /// Gets the explicitly registered mapping profiles in deterministic composition order.
    /// </summary>
    internal IReadOnlyList<Type> ProfileTypes => _profileTypes;

    /// <summary>
    /// Records one mapping profile while preserving first-registration order.
    /// </summary>
    /// <param name="profileType">The concrete Mapster profile type.</param>
    /// <param name="profileKey">The assembly-qualified idempotency key.</param>
    internal void AddProfile(Type profileType, string profileKey)
    {
        if (_profileKeys.Add(profileKey))
        {
            _profileTypes.Add(profileType);
        }
    }
}
