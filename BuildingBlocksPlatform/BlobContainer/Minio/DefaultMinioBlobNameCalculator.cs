using BuildingBlocksPlatform.BlobContainer.Abstract;
using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;


namespace BuildingBlocksPlatform.BlobContainer.Minio;

public class DefaultMinioBlobNameCalculator : IMinioBlobNameCalculator, ITransientDependency
{
    public DefaultMinioBlobNameCalculator()
    {
    }


    public virtual string Calculate(BlobProviderArgs args)
    {
        return $"host/{args.BlobName}";
    }
}