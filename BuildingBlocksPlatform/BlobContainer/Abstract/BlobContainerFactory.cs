


using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobContainerFactory : IBlobContainerFactory, ITransientDependency
{
    public BlobContainerFactory(
        IBlobContainerConfigurationProvider configurationProvider,
        IBlobProviderSelector providerSelector,
        IServiceProvider serviceProvider,
        IBlobNormalizeNamingService blobNormalizeNamingService)
    {
        ConfigurationProvider = configurationProvider;
        ProviderSelector = providerSelector;
        ServiceProvider = serviceProvider;
        BlobNormalizeNamingService = blobNormalizeNamingService;
    }

    protected IBlobProviderSelector ProviderSelector { get; }

    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }


    protected IServiceProvider ServiceProvider { get; }

    protected IBlobNormalizeNamingService BlobNormalizeNamingService { get; }

    public virtual IBlobContainer Create(string name)
    {
        var configuration = ConfigurationProvider.Get(name);

        return new BlobContainer(
            name,
            configuration,
            ProviderSelector.Get(name),
            BlobNormalizeNamingService,
            ServiceProvider
        );
    }
}