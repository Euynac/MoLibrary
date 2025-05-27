using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoLibrary.Tool.Extensions;

public static class AssemblyExtensions
{
    public static IEnumerable<Assembly> WithDomainAssemblies(this Assembly assembly, params string[]? relatedNames)
    {
        yield return assembly;
        if (relatedNames is null) yield break;
        foreach (var domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(p => p.GetName().FullName is { } name && relatedNames.Any(s => name.Contains(s))))
            yield return domainAssembly;
    }
    /// <summary>
    /// Retrieves the related assemblies for the specified entry assembly, optionally filtered by related names. including the entry assembly and any related assemblies that match the specified filter criteria.
    /// </summary>
    /// <param name="entryAssembly">
    /// The entry assembly for which related assemblies are to be retrieved.
    /// </param>
    /// <param name="relatedNames">
    /// An optional array of strings used to filter related assemblies by their names.
    /// </param>
    /// <returns>
    /// An enumerable collection of assemblies, including the entry assembly and any related assemblies
    /// that match the specified filter criteria.
    /// </returns>
    public static IEnumerable<Assembly> GetRelatedAssemblies(this Assembly entryAssembly, params string[]? relatedNames)
    {
        yield return entryAssembly;
        if (relatedNames is null) yield break;


        foreach (var assembly in entryAssembly.GetReferencedAssemblies()
                     .Where(p => relatedNames.Any(s => p.FullName.Contains(s))).Select(Assembly.Load))
        {
            yield return assembly;
        }


        //var hash = entryAssembly.GetReferencedAssemblies()
        //    .Where(p => relatedNames.Any(s => p.FullName.Contains(s))).Select(p=>p.Name).ToHashSet();


        ////该方案依赖MVC
        ////var hash = entryAssembly.CustomAttributes.Where(p => p.AttributeType == typeof(ApplicationPartAttribute))
        ////    .Where(p => p.ConstructorArguments.FirstOrDefault() is {Value: string { } value} &&
        ////                relatedNames.Any(s => value.Contains(s)))
        ////    .Select(p => (string) p.ConstructorArguments.First().Value!).ToHashSet();

        //foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
        //             .Where(p => p.GetName().Name is { } name && hash.Contains(name)))
        //    yield return assembly;
    }
}