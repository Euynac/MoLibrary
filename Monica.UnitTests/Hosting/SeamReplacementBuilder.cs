using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Monica.UnitTests.Hosting;

internal sealed class SeamReplacementBuilder(IServiceCollection services) : ISeamReplacementBuilder
{
    public ISeamReplacementBuilder With<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        services.RemoveAll<TService>();
        services.AddSingleton(instance);
        return this;
    }

    public ISeamReplacementBuilder With<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        services.RemoveAll<TService>();
        services.AddScoped(factory);
        return this;
    }

    public ISeamReplacementBuilder Decorate<TService, TDecorator>()
        where TService : class
        where TDecorator : class, TService
    {
        services.RemoveAll<TService>();
        services.AddScoped<TService, TDecorator>();
        return this;
    }

    public ISeamReplacementBuilder Substitute<TService>(out TService substitute)
        where TService : class
    {
        substitute = NSubstitute.Substitute.For<TService>();
        return With(substitute);
    }
}
