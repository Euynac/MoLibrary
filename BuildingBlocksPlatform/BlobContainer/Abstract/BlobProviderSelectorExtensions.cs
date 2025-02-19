using BuildingBlocksPlatform.Utils;
using JetBrains.Annotations;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public static class BlobProviderSelectorExtensions
{
    public static IBlobProvider Get<TContainer>(
        this IBlobProviderSelector selector)
    {
        Check.NotNull(selector, nameof(selector));

        return selector.Get(BlobContainerNameAttribute.GetContainerName<TContainer>());
    }
}