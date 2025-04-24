namespace MoLibrary.AutoModel.Exceptions;

public class AutoModelSnapshotException(string message) : AutoModelBaseException(message);

public class AutoModelSnapshotNotSupportTypeException(string message, Type fromPropertyType) : AutoModelSnapshotException(message)
{
    /// <summary>
    /// 相关的不支持的字段类型
    /// </summary>
    public Type FromPropertyType { get; } = fromPropertyType;
}