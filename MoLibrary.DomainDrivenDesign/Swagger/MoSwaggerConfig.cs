namespace MoLibrary.DomainDrivenDesign.Swagger;

public class MoSwaggerConfig
{
    /// <summary>
    /// 应用名
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// 接口版本
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 文档描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 服务项目名，用于Swagger文档生成
    /// </summary>
    public string[]? DocumentAssemblies { get; set; }

    /// <summary>
    /// 是否使用认证
    /// </summary>
    public bool UseAuth { get; set; }

}