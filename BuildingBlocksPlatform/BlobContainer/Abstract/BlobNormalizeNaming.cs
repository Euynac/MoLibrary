namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobNormalizeNaming
{
    public BlobNormalizeNaming(string? containerName, string? blobName)
    {
        ContainerName = containerName;
        BlobName = blobName;
    }

    public string? ContainerName { get; }

    public string? BlobName { get; }
}