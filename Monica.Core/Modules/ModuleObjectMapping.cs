using System.Reflection;
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
using Monica.Core.ObjectMapping.Models;
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
        /// Registers one host-owned Mapster configuration and automatically applies concrete <see cref="IRegister"/>
        /// profiles found by Monica's business-type discovery pipeline.
        /// </summary>
        /// <param name="action">Optional configuration for the Mapster compiler and object-mapping endpoints.</param>
        /// <returns>The object-mapping guide for optional explicit registration.</returns>
        /// <remarks>
        /// Profiles outside the host's type-discovery scope can be added explicitly through
        /// <see cref="ModuleObjectMappingGuide.AddProfile{TProfile}"/>.
        /// </remarks>
        public ModuleObjectMappingGuide AddObjectMapping(Action<ModuleObjectMappingOption>? action = null)
        {
            return builder.AddModule<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>(action);
        }
    }
}

/// <summary>
/// Provides the Mapster-based object mapping capability.
/// </summary>
/// <remarks>
/// Each Monica host owns an isolated configuration. Concrete, closed <see cref="IRegister"/> profiles discovered as
/// business types are composed after explicitly registered profiles in dependency-first assembly order.
/// </remarks>
[ModuleKey(BuiltInModuleKey.ObjectMapping)]
public class ModuleObjectMapping(ModuleObjectMappingOption option)
    : WebModuleBase<ModuleObjectMapping, ModuleObjectMappingOption, ModuleObjectMappingGuide>(option),
        IBusinessTypeIterator
{
    private readonly TypeAdapterConfig _mapsterConfig = new();
    private readonly MapsterProfileCatalog _profileCatalog = new();

    /// <inheritdoc />
    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        MapsterCompilerConfigurator.Configure(_mapsterConfig, option);

        services.AddSingleton(_profileCatalog);
        services.AddSingleton(_mapsterConfig);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddScoped<IObjectMapper, MapsterObjectMapper>();
        services.AddSingleton<MapsterMappingInspector>();
        services.AddScoped<ObjectMappingStatusService>();
        services.AddScoped<ObjectMappingFacade>(serviceProvider => new ObjectMappingFacade(
            serviceProvider.GetRequiredService<ObjectMappingStatusService>(),
            serviceProvider.GetRequiredService<ILogger<ObjectMappingFacade>>()));
    }

    /// <inheritdoc />
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            _profileCatalog.Discover(type);
            yield return type;
        }
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection _)
    {
        _profileCatalog.ApplyProfiles(_mapsterConfig, option.ProfileTypes, Application.TypeDependencyOrderer);
        ScheduleCompositionWork(
            "compile-mapster-configuration",
            () => _mapsterConfig.Compile(failFast: false));
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
    /// Explicit profiles execute once in registration order before automatically discovered business profiles.
    /// Use this method for reusable library profiles that are outside the host's business-type scan or for intentional
    /// refinements that require an explicit position. Repeating the same profile type is idempotent within one host.
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
    /// Gets or sets the expression compiler used by the host-owned Mapster configuration. The default uses the
    /// standard LINQ expression compiler. FastExpressionCompiler provides an opt-in startup optimization and rejects
    /// unsupported expressions during eager validation instead of falling back silently.
    /// </summary>
    public ObjectMappingCompilerStrategy CompilerStrategy { get; set; } = ObjectMappingCompilerStrategy.Default;

    /// <summary>
    /// Gets or sets additional assemblies containing base types or extension methods required by the
    /// <see cref="ObjectMappingCompilerStrategy.Debug"/> compiler. The default is empty, and this setting has no
    /// effect for other strategies.
    /// </summary>
    public Assembly[] DebuggerRelatedAssemblies { get; set; } = [];

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
