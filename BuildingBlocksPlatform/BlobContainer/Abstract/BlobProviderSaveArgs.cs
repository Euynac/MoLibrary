using JetBrains.Annotations;
using MoLibrary.Tool.Utils;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobProviderSaveArgs : BlobProviderArgs
{
    public BlobProviderSaveArgs(
        string containerName,
        BlobContainerConfiguration configuration,
        string blobName,
        Stream blobStream,
        bool overrideExisting = false,
        CancellationToken cancellationToken = default)
        : base(
            containerName,
            configuration,
            blobName,
            cancellationToken)
    {
        BlobStream = Check.NotNull(blobStream, nameof(blobStream));
        OverrideExisting = overrideExisting;
    }

    public Stream BlobStream { get; }

    public bool OverrideExisting { get; }
}