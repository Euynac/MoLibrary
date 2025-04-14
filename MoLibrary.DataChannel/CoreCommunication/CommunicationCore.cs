using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.CoreCommunication;


/// <summary>
/// 通信能力核心基类
/// </summary>
public abstract class CommunicationCore : ICommunicationCore
{
    public abstract Task InitAsync();
    public abstract Task DisposeAsync();


    public abstract EConnectionDirection SupportedConnectionDirection();

    /// <summary>
    /// 构建来自于该端的数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual DataContext CreateData(object? data)
    {
        return new DataContext(EntranceType, data);
    }
    
    /// <summary>
    /// 异步发送数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public async Task SendDataAsync(DataContext data)
    {
        DecorateDataContextBeforeSend(data);
        await Pipe.SendDataAsync(data);
    }

    /// <summary>
    /// 异步发送数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public async Task SendDataAsync(object data)
    {
        var dataContext = CreateData(data);
        await SendDataAsync(dataContext);
    }

    /// <summary>
    /// 装饰数据
    /// </summary>
    /// <param name="dataContext"></param>
    protected virtual void DecorateDataContextBeforeSend(DataContext dataContext)
    {

    }
    public void SendData(DataContext data)
    {
        SendDataAsync(data).Wait();
    }
    public void SendData(object data)
    {
        SendDataAsync(data).Wait();
    }

    /// <summary>
    /// 管道实例。初始化后非空
    /// </summary>
    public DataPipeline Pipe { get; set; } = null!;

    /// <summary>
    /// 异步方法接收消息
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual async Task ReceiveDataAsync(DataContext data)
    {
        ReceiveData(data);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 同步方法接收消息
    /// </summary>
    /// <param name="data"></param>
    public virtual void ReceiveData(DataContext data)
    {
    }

    public EDataSource EntranceType { get; set; }
    public abstract dynamic GetMetadata();
}

/// <summary>
/// <inheritdoc cref="CommunicationCore"/>
/// </summary>
/// <typeparam name="TMetadata"></typeparam>
/// <param name="metadata"></param>
public abstract class CommunicationCore<TMetadata>(TMetadata metadata) : CommunicationCore where TMetadata : CommunicationMetadata
{
    public TMetadata Metadata { get; private set; } = metadata;

    public override Task InitAsync()
    {
        return Task.CompletedTask;
    }

    public override Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public override dynamic GetMetadata() => Metadata;
}