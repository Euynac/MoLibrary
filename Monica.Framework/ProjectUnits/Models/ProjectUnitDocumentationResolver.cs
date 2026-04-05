using System.Reflection;
using Monica.Modules;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// Project unit methods metadata helper class
/// </summary>
public static class ProjectUnitDocumentationResolver
{
    /// <summary>
    /// Get the public method metadata of the specified type
    /// </summary>
    /// <param name="type">type</param>
    /// <param name="baseType">Base class types to exclude</param>
    /// <returns>Method metadata list</returns>
    public static List<ProjectUnitMethod> GetPublicMethods(Type type, Type? baseType = null)
    {
        return GetMethods(type, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, baseType);
    }

    /// <summary>
    /// Get method metadata of a specified type
    /// </summary>
    /// <param name="type">type</param>
    /// <param name="bindingFlags">binding flag</param>
    /// <param name="baseType">Base class types to exclude</param>
    /// <returns>Method metadata list</returns>
    public static List<ProjectUnitMethod> GetMethods(Type type, BindingFlags bindingFlags, Type? baseType = null)
    {
        var methods = type.GetMethods(bindingFlags)
            .Where(m => !m.IsSpecialName && // 排除属性的get/set方法等特殊方法
                        m.DeclaringType != typeof(object)); // 排除从Object继承的方法
            

        // If a base class type is specified, base class methods are also excluded
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
    /// Get type XML document information
    /// </summary>
    /// <param name="type">Specify type</param>
    /// <returns>Type description, returns null if none</returns>
    public static string? ExtractTypeDescription(Type type)
    {
        var xmlService = ModuleXmlDocumentation.Singleton;
        return xmlService?.GetTypeDocumentation(type);
    }
    /// <summary>
    /// Extract method description (obtained from XML annotation)
    /// </summary>
    /// <param name="method">method information</param>
    /// <returns>Method description, returns null if none</returns>
    public static string? ExtractMethodDescription(MethodInfo method)
    {
        var xmlService = ModuleXmlDocumentation.Singleton;
        return xmlService?.GetMethodDocumentation(method)?.Summary;
    }
}