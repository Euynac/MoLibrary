using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Framework.Core.Model;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core;

public static class ProjectUnitInfoExtractor
{
    private static readonly ILogger _logger = LogManager.For(typeof(ProjectUnitInfoExtractor));

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
                        _logger.LogError(
                            $"重复的项目单元名，请注意！{unit.Key}与{ProjectUnitStores.ProjectUnitsByName[unit.Type.Name].Type.FullName}重复");
                    }
                }

            }

            yield return type;
        }
    }

    /// <summary>
    /// Extract enumeration information into project unit storage
    /// </summary>
    /// <param name="types">The collection of types to process</param>
    /// <returns>Processed type collection</returns>
    internal static IEnumerable<Type> ExtractEnumInfo(this IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            // Only handle specific enumeration types, excluding open generic types (enumeration types placed under generic classes)
            if (type.IsEnum && type is { IsGenericTypeDefinition: false, ContainsGenericParameters: false})
            {
                ProjectUnitStores.EnumTypes.AddOrIgnore(type.Name, type);
            }
            yield return type;
        }
    }
}
