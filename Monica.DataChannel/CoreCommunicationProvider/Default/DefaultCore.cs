using Monica.DataChannel.CoreCommunication;

namespace Monica.DataChannel.CoreCommunicationProvider.Default;


/// <summary>
/// Default endpoint that actively sends or listens through the channel.
/// </summary>
public class DefaultCore : CommunicationCore<MetadataForDefault>
{
    /// <summary>
    /// Default endpoint that actively sends or listens through the channel.
    /// </summary>
    public DefaultCore(MetadataForDefault metadata) : base(metadata)
    {
    }

    public DefaultCore() : base(new MetadataForDefault())
    {

    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}

/// <summary>
/// Generic default endpoint that requires explicit channel metadata to operate.
/// </summary>
/// <typeparam name="TCore">Concrete endpoint type.</typeparam>
/// <typeparam name="TMetadata">Metadata type used to configure the channel.</typeparam>
/// <param name="metadata">Metadata instance supplied by derived classes.</param>
public class DefaultCore<TCore, TMetadata>(TMetadata metadata) : CommunicationCore<TMetadata>(metadata) where TMetadata : CommunicationMetadata<TCore> where TCore : DefaultCore<TCore, TMetadata>
{
    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}
