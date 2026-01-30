using Monica.Framework.Core.Model;

namespace Monica.Framework.Core.Interfaces;

public interface IHasProjectUnitFactory
{
    /// <summary>
    /// 当前项目单元信息建造工厂
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static abstract ProjectUnit? Factory(FactoryContext context);
}