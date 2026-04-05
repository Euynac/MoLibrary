using Monica.Framework.ProjectUnits.Models;

namespace Monica.Framework.ProjectUnits.Abstractions;

public interface IHasProjectUnitFactory
{
    /// <summary>
    /// Current project unit information construction factory
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static abstract ProjectUnit? Factory(FactoryContext context);
}