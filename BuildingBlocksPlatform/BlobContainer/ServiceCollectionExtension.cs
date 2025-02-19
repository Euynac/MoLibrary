using BuildingBlocksPlatform.BlobContainer.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.BlobContainer;

public static class ServiceCollectionExtension
{
    public static void AddMoBlobContainer(this IServiceCollection services, Action<MoBlobStoringOptions> action)
    {
        services.AddTransient(
            typeof(IBlobContainer<>),
            typeof(BlobContainer<>)
        );

        services.AddTransient(
            typeof(IBlobContainer),
            serviceProvider => serviceProvider
                .GetRequiredService<IBlobContainer<DefaultContainer>>()
        );

        services.Configure(action);
    }
}