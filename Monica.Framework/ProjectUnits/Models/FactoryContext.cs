using Microsoft.Extensions.DependencyInjection;

namespace Monica.Framework.ProjectUnits.Models;

public class FactoryContext
{
    /// <summary>
    /// current type
    /// </summary>
    public required Type Type { get; set; }

    /// <summary>
    /// Service registration container
    /// </summary>
    public required IServiceCollection ServiceCollection { get; set; }
}