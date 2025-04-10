﻿using System.Collections.Concurrent;
using MySqlConnector;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes.RouteRuleEngine;
using ShardingCore.Core.VirtualRoutes.TableRoutes.Abstractions;
using ShardingCore.TableCreator;

namespace MoLibrary.Framework.Features;

/// <summary>
/// 自动创建缺失分表的路由
/// </summary>
/// <typeparam name="TEntity"></typeparam>
[Obsolete("未完成")]
public abstract class AutoCreateDateTimeShardingTableRoute<TEntity> : AbstractShardingOperatorVirtualTableRoute<TEntity, string> where TEntity:class
{
    private readonly IVirtualDataSource _virtualDataSource;
    private readonly IShardingTableCreator _tableCreator;
    private const string Tables = "Tables";
    private const string TABLE_SCHEMA = "TABLE_SCHEMA";
    private const string TABLE_NAME = "TABLE_NAME";

    private const string CurrentTableName = nameof(TEntity);

    private readonly ConcurrentDictionary<string, object?> _tails = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new object();

    protected AutoCreateDateTimeShardingTableRoute(IVirtualDataSource virtualDataSource, IShardingTableCreator tableCreator)
    {
        _virtualDataSource = virtualDataSource;
        _tableCreator = tableCreator;
        InitTails();
    }

    private void InitTails()
    {
        using var connection = new MySqlConnection(_virtualDataSource.DefaultConnectionString);
        connection.Open();
        var database = connection.Database;

        using var dataTable = connection.GetSchema(Tables);
        for (int i = 0; i < dataTable.Rows.Count; i++)
        {
            var schema = dataTable.Rows[i][TABLE_SCHEMA];
            if (database.Equals($"{schema}", StringComparison.OrdinalIgnoreCase))
            {
                var tableName = dataTable.Rows[i][TABLE_NAME]?.ToString() ?? string.Empty;
                if (tableName.StartsWith(CurrentTableName, StringComparison.OrdinalIgnoreCase))
                {
                    //如果没有下划线那么需要CurrentTableName.Length有下划线就要CurrentTableName.Length+1
                    _tails.TryAdd(tableName.Substring(CurrentTableName.Length + 1), null);
                }
            }
        }
    }


    public override string ShardingKeyToTail(object shardingKey)
    {
        return $"{shardingKey}";
    }
    /// <summary>
    /// 如果你是非mysql数据库请自行实现这个方法返回当前类在数据库已经存在的后缀
    /// 仅启动时调用
    /// </summary>
    /// <returns></returns>
    public override List<string> GetTails()
    {
        return _tails.Keys.ToList();
    }

    public override Func<string, bool> GetRouteToFilter(string shardingKey, ShardingOperatorEnum shardingOperator)
    {
        var t = ShardingKeyToTail(shardingKey);
        switch (shardingOperator)
        {
            case ShardingOperatorEnum.Equal: return tail => tail == t;
            default:
            {
#if DEBUG
                Console.WriteLine($"shardingOperator is not equal scan all table tail");
#endif
                return tail => true;
            }
        }
    }

    public override TableRouteUnit RouteWithValue(DataSourceRouteResult dataSourceRouteResult, object shardingKey)
    {
        var shardingKeyToTail = ShardingKeyToTail(shardingKey);
        if (!_tails.TryGetValue(shardingKeyToTail, out var _))
        {
            lock (_lock)
            {
                if (!_tails.TryGetValue(shardingKeyToTail, out var _))
                {

                    try
                    {
                        _tableCreator.CreateTable<TEntity>(_virtualDataSource.DefaultDataSourceName,
                            shardingKeyToTail);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("尝试添加表失败" + ex);
                    }

                    _tails.TryAdd(shardingKeyToTail, null);
                }
            }
        }

        return base.RouteWithValue(dataSourceRouteResult, shardingKey);
    }
}