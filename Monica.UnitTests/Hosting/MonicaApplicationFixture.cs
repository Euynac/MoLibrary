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
    protected virtual void ConfigureModule(IMonicaBuilder builder)
    {
        RegisterStartupModule(builder);
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
        var builder = WebApplication.CreateBuilder();
        ConfigureDefaults(builder.Services);
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(TStartupModule).Assembly);
            });
            ConfigureModule(monica);
        });
        builder.Services.AddMonicaTestSeams();
        ConfigureOverrides(builder.Services);
        _serviceDescriptors = builder.Services.ToList();
        var app = builder.Build();
        app.UseMonica();
        app.MapMonica();
        _host = app;
        _application = app.Services.GetRequiredService<MonicaApplication>();
        await _host.StartAsync();
        _moduleSnapshots = _application.Modules.RuntimeSnapshots.ToList();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        _application = null;
    }

    private ServiceProvider CreateReplacementProvider(Action<ISeamReplacementBuilder> replace)
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var descriptor in _serviceDescriptors)
        {
            if (descriptor.ServiceType == typeof(MonicaApplication))
            {
                services.AddSingleton(_application!);
                continue;
            }

            services.Add(descriptor);
        }

        replace(new SeamReplacementBuilder(services));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    private static void RegisterStartupModule(IMonicaBuilder builder)
    {
        var guideType = FindGuideType(typeof(TStartupModule))
            ?? throw new InvalidOperationException(
                $"Unable to find a ModuleGuide for startup module {typeof(TStartupModule).FullName}.");

        var guideBase = FindGenericGuideBase(guideType)
            ?? throw new InvalidOperationException($"{guideType.FullName} does not expose Monica module metadata.");
        var genericArguments = guideBase.GetGenericArguments();
        var addModuleMethod = typeof(IMonicaBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(IMonicaBuilder.AddModule) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(genericArguments[0], genericArguments[1], genericArguments[2]);

        try
        {
            addModuleMethod.Invoke(builder, [null]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
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
        return FindGenericGuideBase(guideType)?.GetGenericArguments()[0];
    }

    private static Type? FindGenericGuideBase(Type guideType)
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
                return current;
            }
        }

        return null;
    }
}
