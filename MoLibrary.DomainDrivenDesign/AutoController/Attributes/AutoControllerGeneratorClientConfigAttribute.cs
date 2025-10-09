using MoLibrary.DomainDrivenDesign.AutoController.MoRpc;

namespace MoLibrary.DomainDrivenDesign.AutoController.Attributes;

/// <summary>
/// 生成客户端侧调用接口设置
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class AutoControllerGeneratorClientConfigAttribute : Attribute
{
    /// <summary>
    /// 是否添加GRPC实现
    /// </summary>
    public bool AddGrpcImplementations { get; set; } = false;
    /// <summary>
    /// 是否添加HTTP实现
    /// </summary>
    public bool AddHttpImplementations { get; set; } = true;

    /// <summary>
    /// HTTP实现接口类型，为空默认使用 <see cref="MoHttpApi"/>。用于生成时的基类以及获取相关命名空间。若使用自定义实现，需继承自 <see cref="MoHttpApi"/>，且不能有多余的构造参数。
    /// </summary>
    public Type? HttpImplementationType { get; set; }
}