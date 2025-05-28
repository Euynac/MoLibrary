using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core;

public static class ProjectUnitInfoExtractor
{
    internal static IEnumerable<Type> ExtractUnitInfo(this IEnumerable<Type> types, IServiceCollection services)
    {
        foreach (var type in types)
        {
            if (type is {IsClass: true, FullName: not null, IsGenericType: false, IsAbstract: false})
            {
                var unit = ProjectUnit.CreateUnit(new FactoryContext()
                {
                    ServiceCollection = services,
                    Type = type
                });
                if (unit != null)
                {
                    unit.PolishUnitInfo();
                    ProjectUnitStores.ProjectUnitsByFullName.Add(unit.Key, unit);
                    if (!ProjectUnitStores.ProjectUnitsByName.TryAdd(unit.Type.Name, unit))
                    {
                        GlobalLog.LogError(
                            $"重复的项目单元名，请注意！{unit.Key}与{ProjectUnitStores.ProjectUnitsByName[unit.Type.Name].Type.FullName}重复");
                    }
                }

            }

            yield return type;
        }
    }

    /// <summary>
    /// 提取枚举信息到项目单元存储中
    /// </summary>
    /// <param name="types">要处理的类型集合</param>
    /// <returns>处理后的类型集合</returns>
    internal static IEnumerable<Type> ExtractEnumInfo(this IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            // 只处理具体的枚举类型，排除开放泛型类型(放在泛型类下的枚举类型)
            if (type.IsEnum && !type.IsGenericTypeDefinition && !type.ContainsGenericParameters)
            {
                ProjectUnitStores.EnumTypes.AddOrIgnore(type.Name, type);
            }
            yield return type;
        }
    }
}