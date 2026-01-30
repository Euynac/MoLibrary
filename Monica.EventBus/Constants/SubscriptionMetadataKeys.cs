namespace Monica.EventBus.Constants;

/// <summary>
/// 订阅元数据的标准键名
/// </summary>
public static class SubscriptionMetadataKeys
{
    /// <summary>
    /// Action处理器的方法名称
    /// </summary>
    public const string ActionMethodName = "ActionMethodName";

    /// <summary>
    /// Action处理器的声明类型全名
    /// </summary>
    public const string ActionDeclaringType = "ActionDeclaringType";

    /// <summary>
    /// Action处理器的方法签名
    /// </summary>
    public const string ActionMethodSignature = "ActionMethodSignature";

    /// <summary>
    /// Action处理器的方法是否为静态方法
    /// </summary>
    public const string ActionIsStatic = "ActionIsStatic";
}
