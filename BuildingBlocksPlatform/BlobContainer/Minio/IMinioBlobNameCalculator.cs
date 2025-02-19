using BuildingBlocksPlatform.BlobContainer.Abstract;

namespace BuildingBlocksPlatform.BlobContainer.Minio;

public interface IMinioBlobNameCalculator
{
    string Calculate(BlobProviderArgs args);
}