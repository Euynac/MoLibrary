namespace MoLibrary.DomainDrivenDesign.AutoController.Settings;

public class MoCrudControllerOption
{
    /// <summary>
    /// 自动CRUD路径前缀
    /// </summary>
    public string RoutePath { get; set; } = "api";
    /// <summary>
    /// Controller自动注册后缀
    /// </summary>
    public string CrudControllerPostfix { get; set; } = "CrudService";
}