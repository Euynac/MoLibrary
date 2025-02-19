using BuildingBlocksPlatform.Utils;
using JetBrains.Annotations;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public abstract class BlobProviderArgs
{
    protected BlobProviderArgs(
        string containerName,
        BlobContainerConfiguration configuration,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ContainerName = Check.NotNullOrWhiteSpace(containerName, nameof(containerName));
        Configuration = Check.NotNull(configuration, nameof(configuration));
        BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        CancellationToken = cancellationToken;
    }

    public string ContainerName { get; }

    public BlobContainerConfiguration Configuration { get; }

    public string BlobName { get; }

    public CancellationToken CancellationToken { get; }
}