namespace Monica.Core.ExceptionHandler;

public interface IMoModuleOptionUseGlobalException
{
    /// <summary>
    /// 是否禁用全局异常处理器
    /// </summary>
    bool DisableGlobalExceptionHandler { get; set; }
}