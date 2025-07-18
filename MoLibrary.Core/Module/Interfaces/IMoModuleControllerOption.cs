using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Core.Module.Interfaces;

/// <summary>
/// Defines the options for a module in the MoLibrary.
/// </summary>
public interface IMoModuleControllerOption
{
    /// <summary>
    /// Gets the Swagger group name for the module.
    /// </summary>
    /// <returns></returns>
    public string GetSwaggerGroupName();
    /// <summary>
    /// Gets a value indicating whether controllers are disabled for the module.
    /// </summary>
    /// <returns><c>true</c> if controllers are disabled; otherwise, <c>false</c>.</returns>
    public bool GetIsControllerDisabled();

    /// <summary>
    /// Gets a value indicating whether the module is visible in Swagger.
    /// </summary>
    /// <returns><c>true</c> if the module is visible in Swagger; otherwise, <c>false</c>.</returns>
    public bool GetIsVisibleInSwagger();

    /// <summary>
    /// Final controller route template. Hint: if your controller method route starts with "/", then this template will be ignored!
    /// </summary>
    /// <typeparam name="TController"></typeparam>
    /// <returns></returns>
    public string GetControllerRouteTemplate<TController>() where TController : MoModuleControllerBase;

    /// <summary>
    /// 获取完整的路由地址，合并Controller路由模板与指定路径
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <param name="path">要合并的路径</param>
    /// <returns>完整的路由地址</returns>
    public string GetRoute<TController>(string path) where TController : MoModuleControllerBase;
}
