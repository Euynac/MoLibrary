namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public interface IBlobProvider
{
    Task SaveAsync(BlobProviderSaveArgs args);

    Task<bool> DeleteAsync(BlobProviderDeleteArgs args);

    Task<bool> ExistsAsync(BlobProviderExistsArgs args);

    Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args);
}