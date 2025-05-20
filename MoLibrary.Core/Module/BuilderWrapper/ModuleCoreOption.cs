namespace MoLibrary.Core.Module.BuilderWrapper;

public class ModuleCoreOption
{
    /// <summary>
    /// 启用立即注册特性。注意对于有嵌套依赖的模块注册慎用，会使得后续的这些模块配置失效，因为已经被注册。
    /// </summary>
    public bool EnableRegisterInstantly { get; set; }
}