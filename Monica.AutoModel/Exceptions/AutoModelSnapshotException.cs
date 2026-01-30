namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Snapshot 构建和配置错误
/// </summary>
public class AutoModelSnapshotException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Snapshot 不支持的类型异常
/// </summary>
public class AutoModelSnapshotNotSupportTypeException(
    string displayMessage,
    string technicalDetail,
    Type fromPropertyType)
    : AutoModelSnapshotException(displayMessage, technicalDetail)
{
    /// <summary>
    /// 相关的不支持的字段类型
    /// </summary>
    public Type FromPropertyType { get; } = fromPropertyType;
}