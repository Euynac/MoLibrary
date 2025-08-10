using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Framework.Core.Model;

public class FactoryContext
{
    /// <summary>
    /// 当前类型
    /// </summary>
    public required Type Type { get; set; }

    /// <summary>
    /// 服务注册容器
    /// </summary>
    public required IServiceCollection ServiceCollection { get; set; }
}