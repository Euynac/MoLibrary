namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Snapshot construction or configuration error.
/// </summary>
public class AutoModelSnapshotException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Exception for unsupported types in a snapshot.
/// </summary>
public class AutoModelSnapshotNotSupportTypeException(
    string displayMessage,
    string technicalDetail,
    Type fromPropertyType)
    : AutoModelSnapshotException(displayMessage, technicalDetail)
{
    /// <summary>
    /// Unsupported field type related to the error.
    /// </summary>
    public Type FromPropertyType { get; } = fromPropertyType;
}
