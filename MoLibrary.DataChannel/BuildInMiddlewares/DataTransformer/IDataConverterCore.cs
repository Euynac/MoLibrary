namespace MoLibrary.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// 数据协议双向(Bidirectional)转换核心接口
/// </summary>
public interface IDataBiConverterCore<T1, T2> : IDataConverterCore
{
    public T2 Convert(T1 data);
    public T1 Convert(T2 data);
}

/// <summary>
/// 数据协议单向(Unidirectional)转换核心接口
/// </summary>
public interface IDataUniConverterCore<in TSource, out TDestination> : IDataConverterCore
{
    public TDestination Convert(TSource data);
}

public interface IDataConverterCore
{
    bool CanConvert(Type source);
    object? Convert(object? source);
}