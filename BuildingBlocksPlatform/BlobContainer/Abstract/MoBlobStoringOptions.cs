namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class MoBlobStoringOptions
{
    public MoBlobStoringOptions()
    {
        Containers = new BlobContainerConfigurations();
    }

    public BlobContainerConfigurations Containers { get; }
}