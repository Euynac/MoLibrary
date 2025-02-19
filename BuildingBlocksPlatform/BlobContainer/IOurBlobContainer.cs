namespace BuildingBlocksPlatform.BlobContainer;

public interface IOurBlobContainer
{
    Task<string> SaveAsync(string moduleName, string fileName, byte[] bytes,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetAllBytesOrNullAsync(string fileId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken = default);
}

