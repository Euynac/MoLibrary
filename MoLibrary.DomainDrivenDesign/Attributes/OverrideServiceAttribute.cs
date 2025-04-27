using MoLibrary.DomainDrivenDesign.AutoCrud;

namespace MoLibrary.DomainDrivenDesign.Attributes;


/// <summary>
/// 指示该方法是最终需要服务化的方法，用于<see cref="MoCrudAppService{TEntity,TEntityDto,TKey,TGetListInput,TRepository}"/>等应用服务使用new或不同签名方法来覆盖基类的相同方法签名的方法，即扩展了override关键字，使其能重写返回值。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OverrideServiceAttribute(int order = 0) : Attribute
{
    /// <summary>
    /// 当当前类及其父类有多个相同方法签名时，采用最大的作为接口进行生成。
    /// </summary>
    public int Order { get; set; } = order;
}