using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;

namespace MoLibrary.Generators.AutoController.Helpers;

internal static class AttributeHelper
{
    /// <summary>
    /// Extracts the Route attribute value from a class declaration.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>The route value or null if not found</returns>
    public static string? ExtractRouteAttribute(ClassDeclarationSyntax classDeclaration)
    {
        var routeAttribute = classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == GeneratorConstants.AttributeNames.Route);

        if (routeAttribute == null) 
            return null;

        var routeArg = routeAttribute.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"');
        return string.IsNullOrEmpty(routeArg) ? null : routeArg;
    }

    /// <summary>
    /// Extracts all Tags attribute values from a class declaration.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>List of distinct tag values</returns>
    public static List<string> ExtractTagsAttributes(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(attr => attr.Name.ToString() == GeneratorConstants.AttributeNames.Tags)
            .Select(attr => attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"'))
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Cast<string>()
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Determines if an attribute name represents an HTTP method attribute.
    /// </summary>
    /// <param name="attributeName">The attribute name to check</param>
    /// <returns>True if it's an HTTP method attribute</returns>
    public static bool IsHttpMethodAttribute(string attributeName)
    {
        return attributeName switch
        {
            GeneratorConstants.AttributeNames.HttpPost or 
            GeneratorConstants.AttributeNames.HttpPut or 
            GeneratorConstants.AttributeNames.HttpGet or 
            GeneratorConstants.AttributeNames.HttpDelete or 
            GeneratorConstants.AttributeNames.HttpPatch => true,
            _ => false
        };
    }

    /// <summary>
    /// Extracts the HTTP method attribute and its route from a method declaration.
    /// </summary>
    /// <param name="method">The method declaration to analyze</param>
    /// <returns>Tuple containing the HTTP method name and route</returns>
    public static (string httpMethod, string? route) ExtractHttpMethodAndRoute(MethodDeclarationSyntax method)
    {
        var attr = method.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => IsHttpMethodAttribute(a.Name.ToString()));

        if (attr == null)
            return (string.Empty, null);

        var httpMethod = attr.Name.ToString();
        var routeArgument = attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"');
        return (httpMethod, routeArgument);
    }

    /// <summary>
    /// Checks if a method parameter has the FromForm attribute.
    /// </summary>
    /// <param name="method">The method declaration to analyze</param>
    /// <returns>True if any parameter has FromForm attribute</returns>
    public static bool HasFromFormAttribute(MethodDeclarationSyntax method)
    {
        return method.ParameterList.Parameters.FirstOrDefault()
            ?.AttributeLists.SelectMany(al => al.Attributes)
            .Any(attr => attr.Name.ToString() == GeneratorConstants.AttributeNames.FromForm) ?? false;
    }

    /// <summary>
    /// Determines the appropriate binding attribute based on HTTP method and form attribute presence.
    /// </summary>
    /// <param name="httpMethodAttribute">The HTTP method attribute name</param>
    /// <param name="hasFromForm">Whether the parameter has FromForm attribute</param>
    /// <returns>The binding attribute name (FromQuery, FromForm, or FromBody)</returns>
    public static string DetermineBindingAttribute(string httpMethodAttribute, bool hasFromForm)
    {
        return httpMethodAttribute == GeneratorConstants.AttributeNames.HttpGet 
            ? GeneratorConstants.AttributeNames.FromQuery
            : hasFromForm 
                ? GeneratorConstants.AttributeNames.FromForm 
                : GeneratorConstants.AttributeNames.FromBody;
    }
}