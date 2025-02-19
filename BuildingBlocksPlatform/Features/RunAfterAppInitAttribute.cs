namespace BuildingBlocksPlatform.Features;
/// <summary>
/// 指示该方法是用于在AppInit后执行的Static构造方法
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public class RunAfterAppInitAttribute : Attribute
{
    
}