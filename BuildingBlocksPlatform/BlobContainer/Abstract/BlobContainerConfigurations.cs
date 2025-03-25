using JetBrains.Annotations;
using MoLibrary.Tool.Utils;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobContainerConfigurations
{
    private readonly Dictionary<string, BlobContainerConfiguration> _containers;

    public BlobContainerConfigurations()
    {
        _containers = new Dictionary<string, BlobContainerConfiguration>
        {
            //Add default container
            [BlobContainerNameAttribute.GetContainerName<DefaultContainer>()] = new()
        };
    }

    private BlobContainerConfiguration Default => GetConfiguration<DefaultContainer>();

    public BlobContainerConfigurations Configure<TContainer>(
        Action<BlobContainerConfiguration> configureAction)
    {
        return Configure(
            BlobContainerNameAttribute.GetContainerName<TContainer>(),
            configureAction
        );
    }

    public BlobContainerConfigurations Configure(
        string name,
        Action<BlobContainerConfiguration> configureAction)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.NotNull(configureAction, nameof(configureAction));

        configureAction(
            _containers.GetOrAdd(
                name,
                () => new BlobContainerConfiguration(Default)
            )
        );

        return this;
    }

    public BlobContainerConfigurations ConfigureDefault(Action<BlobContainerConfiguration> configureAction)
    {
        configureAction(Default);
        return this;
    }

    public BlobContainerConfigurations ConfigureAll(Action<string, BlobContainerConfiguration> configureAction)
    {
        foreach (var container in _containers) configureAction(container.Key, container.Value);

        return this;
    }

    public BlobContainerConfiguration GetConfiguration<TContainer>()
    {
        return GetConfiguration(BlobContainerNameAttribute.GetContainerName<TContainer>());
    }

    public BlobContainerConfiguration GetConfiguration(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        return _containers.GetOrDefault(name) ??
               Default;
    }
}