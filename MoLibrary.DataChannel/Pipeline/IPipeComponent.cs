namespace MoLibrary.DataChannel.Pipeline;

public interface IPipeComponent
{
    
    /// <summary>
    /// 获取管道组件元数据(配置数据等)
    /// </summary>
    /// <returns></returns>
    public dynamic GetMetadata();
}