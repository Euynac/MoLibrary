using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Monica.Testing.Hosting;

/// <summary>
/// Applies final boundary replacements to a scenario service collection before host build.
/// </summary>
internal sealed class SeamReplacementBuilder(IServiceCollection services) : ISeamReplacementBuilder
{
    private readonly Dictionary<string, HttpClient> _httpClients = new(StringComparer.Ordinal);

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

    public ISeamReplacementBuilder Substitute<TService>(out TService substitute)
        where TService : class
    {
        substitute = NSubstitute.Substitute.For<TService>();
        return With(substitute);
    }

    public ISeamReplacementBuilder WithHttpClient(string name, HttpClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        _httpClients[name] = client;
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(
            new Dictionary<string, HttpClient>(_httpClients, StringComparer.Ordinal)));
        return this;
    }
}
