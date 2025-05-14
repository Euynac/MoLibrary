namespace MoLibrary.BackgroundJob.Attributes;


/// <summary>
/// 用于标记不需要自动注册的类
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DisableAutoRegisterAttribute : Attribute
{
  
}