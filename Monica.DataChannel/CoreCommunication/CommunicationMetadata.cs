namespace Monica.DataChannel.CoreCommunication;

public abstract class CommunicationMetadata
{
    /// <summary>
    /// <inheritdoc cref="ECommunicationType"/>
    /// </summary>
    public ECommunicationType Type { get; set; } 
    /// <summary>
    /// Gets or sets the communication direction.
    /// </summary>
    public EConnectionDirection Direction { get; set; }
    public abstract Type GetCommunicationCoreType();
    /// <summary>
    /// Validates the metadata or enriches it with derived values after creation.
    /// Throw an exception directly when validation fails.
    /// </summary>
    public virtual void EnrichOrValidate()
    {

    }
}

public abstract class CommunicationMetadata<TCore> : CommunicationMetadata where TCore : CommunicationCore
{
    public override Type GetCommunicationCoreType() => typeof(TCore);
}

/// <summary>
/// Supported connection directions.
/// </summary>
public enum EConnectionDirection
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
public enum ECommunicationType
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
