namespace Monica.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// Core contract for bidirectional data conversion.
/// </summary>
public interface IDataBiConverterCore<T1, T2> : IDataConverterCore
{
    public T2 Convert(T1 data);
    public T1 Convert(T2 data);
}

/// <summary>
/// Core contract for unidirectional data conversion.
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
