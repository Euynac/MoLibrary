namespace MoLibrary.Core.Module;

public interface IWantIterateBusinessTypes
{
    /// <summary>
    /// 迭代业务类型
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types);
}
