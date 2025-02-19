namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public interface IBlobNamingNormalizer
{
    string NormalizeContainerName(string containerName);

    string NormalizeBlobName(string blobName);
}