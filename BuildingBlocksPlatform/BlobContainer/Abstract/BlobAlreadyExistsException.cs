namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobAlreadyExistsException : Exception
{
    public BlobAlreadyExistsException()
    {
    }

    public BlobAlreadyExistsException(string message)
        : base(message)
    {
    }

    public BlobAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}