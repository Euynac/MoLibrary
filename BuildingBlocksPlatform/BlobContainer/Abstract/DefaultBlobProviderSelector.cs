using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using JetBrains.Annotations;
using MoLibrary.Tool.Utils;

namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class DefaultBlobProviderSelector : IBlobProviderSelector, ITransientDependency
{
    public DefaultBlobProviderSelector(
        IBlobContainerConfigurationProvider configurationProvider,
        IEnumerable<IBlobProvider> blobProviders)
    {
        ConfigurationProvider = configurationProvider;
        BlobProviders = blobProviders;
    }

    protected IEnumerable<IBlobProvider> BlobProviders { get; }

    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    public virtual IBlobProvider Get(string containerName)
    {
        Check.NotNull(containerName, nameof(containerName));

        var configuration = ConfigurationProvider.Get(containerName);

        if (!BlobProviders.Any())
            throw new Exception(
                "No BLOB Storage provider was registered! At least one provider must be registered to be able to use the BLOB Storing System.");

        if (configuration.ProviderType == null)
            throw new Exception(
                "No BLOB Storage provider was used! At least one provider must be configured to be able to use the BLOB Storing System.");

        foreach (var provider in BlobProviders)
            if (ProxyHelper.GetUnProxiedType(provider).IsAssignableTo(configuration.ProviderType))
                return provider;

        throw new Exception(
            $"Could not find the BLOB Storage provider with the type ({configuration.ProviderType.AssemblyQualifiedName}) configured for the container {containerName} and no default provider was set."
        );
    }
}