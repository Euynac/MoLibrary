namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Exception thrown when snapshot construction or configuration fails.
/// </summary>
public class AutoModelSnapshotException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Exception thrown when a snapshot encounters an unsupported field type.
/// </summary>
public class AutoModelSnapshotNotSupportTypeException(
    string displayMessage,
    string technicalDetail,
    Type fromPropertyType)
    : AutoModelSnapshotException(displayMessage, technicalDetail)
{
    /// <summary>
    /// Unsupported field type involved in the error.
    /// </summary>
    public Type FromPropertyType { get; } = fromPropertyType;
}
