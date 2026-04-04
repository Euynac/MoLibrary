using Monica.DataChannel.Abstractions.Communication;

namespace Monica.DataChannel.Providers.Default;


/// <summary>
/// Default endpoint that actively sends or listens through the channel.
/// </summary>
public class DefaultChannelEndpoint : CommunicationEndpointBase<DefaultEndpointOptions>
{
    /// <summary>
    /// Default endpoint that actively sends or listens through the channel.
    /// </summary>
    public DefaultChannelEndpoint(DefaultEndpointOptions metadata) : base(metadata)
    {
    }

    public DefaultChannelEndpoint() : base(new DefaultEndpointOptions())
    {

    }

    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }
}

/// <summary>
/// Generic default endpoint that requires explicit channel metadata to operate.
/// </summary>
/// <typeparam name="TCore">Concrete endpoint type.</typeparam>
/// <typeparam name="TMetadata">Metadata type used to configure the channel.</typeparam>
/// <param name="metadata">Metadata instance supplied by derived classes.</param>
public class DefaultChannelEndpoint<TCore, TMetadata>(TMetadata metadata) : CommunicationEndpointBase<TMetadata>(metadata) where TMetadata : CommunicationOptions<TCore> where TCore : DefaultChannelEndpoint<TCore, TMetadata>
{
    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }
}
