using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using Microsoft.Extensions.Options;

namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class DefaultBlobContainerConfigurationProvider : IBlobContainerConfigurationProvider, ITransientDependency
{
    public DefaultBlobContainerConfigurationProvider(IOptions<MoBlobStoringOptions> options)
    {
        Options = options.Value;
    }

    protected MoBlobStoringOptions Options { get; }

    public virtual BlobContainerConfiguration Get(string name)
    {
        return Options.Containers.GetConfiguration(name);
    }
}