namespace MoLibrary.DomainDrivenDesign.AutoController.Settings;

public class MoAutoControllerOption
{

    /// <summary>
    /// 自动CRUD路径前缀
    /// </summary>
    public string RoutePath { get; set; } = "api";
    /// <summary>
    /// Controller自动注册后缀
    /// </summary>
    public static string AutoControllerPostfix { get; set; } = "AppService";
}