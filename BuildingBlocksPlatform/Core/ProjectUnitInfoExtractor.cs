using BuildingBlocksPlatform.Core.Model;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features;

namespace BuildingBlocksPlatform.Core;

public static class ProjectUnitInfoExtractor
{
    internal static IEnumerable<Type> ExtractUnitInfo(this IEnumerable<Type> types, IServiceCollection services)
    {
        foreach (var type in types)
        {
            if(type is { IsClass: true, FullName: {  } full})
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
    internal static IEnumerable<Type> ExtractEnumInfo(this IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type.IsEnum)
            {
                ProjectUnitStores.EnumTypes.AddOrIgnore(type.Name, type);
            }
            yield return type;
        }
    }
}