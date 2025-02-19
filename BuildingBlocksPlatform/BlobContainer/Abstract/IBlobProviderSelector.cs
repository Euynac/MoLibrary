using JetBrains.Annotations;

namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public interface IBlobProviderSelector
{
    IBlobProvider Get(string containerName);
}