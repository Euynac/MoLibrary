namespace Monica.DataChannel.Abstractions.Communication;

public abstract class CommunicationOptions
{
    /// <summary>
    /// <inheritdoc cref="CommunicationType"/>
    /// </summary>
    public CommunicationType Type { get; set; } 
    /// <summary>
    /// Gets or sets the communication direction.
    /// </summary>
    public ConnectionDirection Direction { get; set; }
    public abstract Type GetCommunicationCoreType();
    /// <summary>
    /// Validates the metadata or enriches it with derived values after creation.
    /// Throw an exception directly when validation fails.
    /// </summary>
    public virtual void EnrichOrValidate()
    {

    }
}

public abstract class CommunicationOptions<TCore> : CommunicationOptions where TCore : CommunicationEndpointBase
{
    public override Type GetCommunicationCoreType() => typeof(TCore);
}

/// <summary>
/// Supported connection directions.
/// </summary>
public enum ConnectionDirection
{
    None,
    /// <summary>
    /// Can receive data.
    /// </summary>
    Input,
    /// <summary>
    /// Can send data.
    /// </summary>
    Output,
    /// <summary>
    /// Supports bidirectional communication.
    /// </summary>
    InputAndOutput
}

/// <summary>
/// Communication types.
/// </summary>
public enum CommunicationType
{
    None,
    HTTP,
    TCP,
    UDP,
    MQ,
    SQL,
    Serial,
    Trigger
}
