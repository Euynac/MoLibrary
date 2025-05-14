using Microsoft.EntityFrameworkCore;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Repository.EntityInterfaces;

namespace MoLibrary.Repository.Modules;

public class ModuleRepositoryOption : MoModuleOption<ModuleRepository>
{
    /// <summary>
    /// Use User-defined function mapping to filter data.
    /// https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
    /// </summary>
    public bool UseDbFunction { get; set; }

    /// <summary>
    /// 自动注册DbContext Factory
    /// </summary>
    public bool UseDbContextFactory { get; set; }

    /// <summary>
    /// 是否启用敏感数据日志。默认为null，表示当环境为Development时启用。
    /// </summary>
    public bool? EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// 禁用实体 <see cref="IHasEntitySelfConfig{TEntity}"/> 功能，当不使用此功能时可关闭
    /// </summary>
    public bool DisableEntitySelfConfiguration { get; set; }

    /// <summary>
    /// 禁用实体原生配置 <see cref="IEntityTypeConfiguration{TEntity}"/> 功能，当不使用<see cref="MoDbContext{TDbContext}"/>提供的此接口自动注册功能可关闭
    /// </summary>
    public bool DisableEntitySeparateConfiguration { get; set; }

    /// <summary>
    /// 并发令牌最大长度
    /// </summary>
    public static int ConcurrencyStampMaxLength = 40;
}