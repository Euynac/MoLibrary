using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services;
using Monica.UnitTests.Modularity;
using Xunit;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Boots a Monica module graph once for a sociable test collection.
/// </summary>
/// <typeparam name="TStartupModule">The startup module for the test collection.</typeparam>
public class MonicaApplicationFixture<TStartupModule> : IAsyncLifetime, IAsyncDisposable
    where TStartupModule : IModule
{
    private MonicaApplication? _application;
    private IHost? _host;
    private List<ServiceDescriptor> _serviceDescriptors = [];
    private IReadOnlyList<ModuleRuntimeSnapshot> _moduleSnapshots = [];

    /// <summary>
    /// Gets the root service provider for the booted module graph.
    /// </summary>
    public IServiceProvider Services
    {
        get
        {
            EnsureInitialized();
            return _host!.Services;
        }
    }

    /// <summary>
    /// Gets a snapshot of modules registered during fixture boot.
    /// </summary>
    public IReadOnlyList<ModuleRuntimeSnapshot> ModuleSnapshots
    {
        get
        {
            EnsureInitialized();
            return _moduleSnapshots;
        }
    }

    /// <summary>
    /// Configures deterministic defaults before module registration.
    /// </summary>
    protected virtual void ConfigureDefaults(IServiceCollection services)
    {
        services.AddMonicaTestSeams();
    }

    /// <summary>
    /// Allows derived collection fixtures to register the startup module and its guide options.
    /// </summary>
    protected virtual void ConfigureModule(IHostApplicationBuilder builder)
    {
        RegisterStartupModule();
    }

    /// <summary>
    /// Applies final service overrides after Monica's module graph and default test seams have been registered.
    /// Override this when a collection fixture needs to replace a service that the default seams also touch.
    /// </summary>
    protected virtual void ConfigureOverrides(IServiceCollection services)
    {
    }

    /// <summary>
    /// Creates a new per-test scope with optional seam replacements.
    /// </summary>
    public ITestScope NewScope(Action<ISeamReplacementBuilder>? replace = null)
    {
        EnsureInitialized();
        var replacementProvider = replace is null ? null : CreateReplacementProvider(replace);
        var provider = replacementProvider ?? Services;
        return new TestScope(
            _application!.Activate(),
            provider.CreateAsyncScope(),
            replacementProvider,
            replacementProvider,
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates a new per-test scope with optional seam replacements.
    /// </summary>
    public Task<ITestScope> NewScopeAsync(Action<ISeamReplacementBuilder>? replace = null)
    {
        return Task.FromResult(NewScope(replace));
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _application = MonicaApplication.CreateScoped();
        ModuleTestScope.Reset();

        Mo.ConfigTypeDiscovery(options =>
        {
            options.ExcludeDefault();
            options.Add(typeof(TStartupModule).Assembly);
        });

        var builder = WebApplication.CreateBuilder();
        ConfigureDefaults(builder.Services);
        ConfigureModule(builder);
        builder.UseMonica();
        builder.Services.AddMonicaTestSeams();
        ConfigureOverrides(builder.Services);
        _serviceDescriptors = builder.Services.ToList();
        var app = builder.Build();
        app.UseMonica();
        app.MapMonica();
        _host = app;
        await _host.StartAsync();
        _moduleSnapshots = ModuleRegistry.ModuleSnapshots.ToList();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        _application?.ResetModuleState();
        _application?.ConfigureTypeDiscovery();
        _application?.Dispose();
    }

    private ServiceProvider CreateReplacementProvider(Action<ISeamReplacementBuilder> replace)
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var descriptor in _serviceDescriptors)
        {
            services.Add(descriptor);
        }

        replace(new SeamReplacementBuilder(services));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    private static void RegisterStartupModule()
    {
        var guideType = FindGuideType(typeof(TStartupModule))
            ?? throw new InvalidOperationException(
                $"Unable to find a ModuleGuide for startup module {typeof(TStartupModule).FullName}.");

        if (Activator.CreateInstance(guideType) is not ModuleGuide guide)
        {
            throw new InvalidOperationException($"{guideType.FullName} must derive from {typeof(ModuleGuide).FullName}.");
        }

        var registerMethod = guideType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(static method => method.Name == "Register" && method.GetParameters().Length <= 1);
        if (registerMethod is null)
        {
            throw new InvalidOperationException($"{guideType.FullName} does not expose a Register method.");
        }

        registerMethod.Invoke(guide, registerMethod.GetParameters().Length == 0 ? [] : [null]);
    }

    private static Type? FindGuideType(Type startupModuleType)
    {
        return startupModuleType.Assembly
            .GetTypes()
            .FirstOrDefault(type =>
                type is { IsAbstract: false } &&
                typeof(ModuleGuide).IsAssignableFrom(type) &&
                GetGenericModuleType(type) == startupModuleType);
    }

    private void EnsureInitialized()
    {
        if (_host is null || _application is null)
        {
            throw new InvalidOperationException($"{GetType().Name} has not been initialized by xUnit.");
        }
    }

    private static Type? GetGenericModuleType(Type guideType)
    {
        var current = guideType;
        while (current.BaseType is not null)
        {
            current = current.BaseType;
            if (!current.IsGenericType)
            {
                continue;
            }

            var genericDefinition = current.GetGenericTypeDefinition();
            if (genericDefinition == typeof(ModuleGuide<,,>) ||
                genericDefinition == typeof(WebModuleGuide<,,>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
