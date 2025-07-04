namespace MoLibrary.Core.Features.MoTimekeeper;

public interface IMoTimekeeperFactory
{
    /// <summary>
    /// 自动结束的计时器
    /// </summary>
    /// <param name="key"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public AutoTimekeeper CreateAutoTimer(string key, string? content = null);

    /// <summary>
    /// 普通计时器，可以手动开始和结束
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public NormalTimekeeper CreateNormalTimer(string key);
}