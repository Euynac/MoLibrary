using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel;

public class DataChannel(DataPipeline pipeline)
{
    public DataPipeline Pipe { get; } = pipeline;
    public string Id => Pipe.Id;
    /// <summary>
    /// 重新初始化
    /// </summary>
    public async Task<Res> ReInitialize()
    {
        return await Pipe.InitAsync();
    }
}