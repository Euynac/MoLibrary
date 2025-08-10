using System.Reflection;
using MoLibrary.Core.Modules;
using MoLibrary.Framework.Modules;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 项目单元方法元数据辅助类
/// </summary>
public static class ProjectUnitXmlDocHelper
{
    /// <summary>
    /// 获取指定类型的公共方法元数据
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="baseType">要排除的基类类型</param>
    /// <returns>方法元数据列表</returns>
    public static List<ProjectUnitMethod> GetPublicMethods(Type type, Type? baseType = null)
    {
        return GetMethods(type, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, baseType);
    }

    /// <summary>
    /// 获取指定类型的方法元数据
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="bindingFlags">绑定标志</param>
    /// <param name="baseType">要排除的基类类型</param>
    /// <returns>方法元数据列表</returns>
    public static List<ProjectUnitMethod> GetMethods(Type type, BindingFlags bindingFlags, Type? baseType = null)
    {
        var methods = type.GetMethods(bindingFlags)
            .Where(m => !m.IsSpecialName && // 排除属性的get/set方法等特殊方法
                        m.DeclaringType != typeof(object)); // 排除从Object继承的方法
            

        // 如果指定了基类类型，也排除基类的方法
        if (baseType != null)
        {
            methods = methods.Where(m => m.DeclaringType != baseType);
        }

        var unitMethods = new List<ProjectUnitMethod>();

        foreach (var method in methods)
        {
            var unitMethod = new ProjectUnitMethod
            {
                MethodInfo = method,
                Description = ExtractMethodDescription(method)
            };
            unitMethods.Add(unitMethod);
        }

        return unitMethods;
    }
    /// <summary>
    /// 获取类型的XML文档信息
    /// </summary>
    /// <param name="type">指定类型</param>
    /// <returns>类型描述，如果没有则返回null</returns>
    public static string? ExtractTypeDescription(Type type)
    {
        var xmlService = ModuleXmlDocumentation.Singleton;
        return xmlService?.GetTypeDocumentation(type);
    }
    /// <summary>
    /// 提取方法描述（从XML注释中获取）
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>方法描述，如果没有则返回null</returns>
    public static string? ExtractMethodDescription(MethodInfo method)
    {
        var xmlService = ModuleXmlDocumentation.Singleton;
        return xmlService?.GetMethodDocumentation(method)?.Summary;
    }
}