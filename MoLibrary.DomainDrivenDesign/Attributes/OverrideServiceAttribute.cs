namespace MoLibrary.DomainDrivenDesign.Attributes;


/// <summary>
/// 指示该方法是最终需要服务化的方法，用于AutoCrud等应用服务使用new或不同签名方法来覆盖Abp基类的相同ActionName的方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OverrideServiceAttribute(int order = 0) : Attribute
{
    /// <summary>
    /// 当有多个时，采用最大的
    /// </summary>
    public int Order { get; set; } = order;
}