using Monica.Framework.Core.Model;

namespace Monica.Framework.Core.Interfaces;

public interface IHasProjectUnitFactory
{
    /// <summary>
    /// Current project unit information construction factory
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static abstract ProjectUnit? Factory(FactoryContext context);
}