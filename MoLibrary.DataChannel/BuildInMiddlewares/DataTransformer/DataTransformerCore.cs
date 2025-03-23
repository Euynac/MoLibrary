using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// 数据协议转换中间件基类
/// </summary>
public abstract class DataTransformerMiddlewareBase : PipeTransformMiddlewareBase, IDataConverterCore
{
    public override DataContext Pass(DataContext context)
    {
        if (context.Data == null) return context;
        if (context.SpecifiedType is { } type && CanConvert(type) && context.Data is { } data)
        {
            var convertedData = Convert(data);
            if (convertedData == null)
            {
                //TODO 应需要返回转换失败原因？
                context.SpecifiedType = null;
                return context;
            }
            context.Data = convertedData;
            context.SpecifiedType = convertedData.GetType();
            context.DataType = SetDataContextType(convertedData.GetType());
        }

        return context;
    }

    public virtual EDataType SetDataContextType(Type outType)
    {
        if (outType == typeof(string))
        {
            return EDataType.String;
        }
        if (outType == typeof(byte[]))
        {
            return EDataType.Bytes;
        }

        return EDataType.Custom;
    }

    public abstract bool CanConvert(Type source);
    public abstract object? Convert(object? source);
}

/// <summary>
/// 单向数据协议转换中间件基类
/// </summary>
/// <typeparam name="TConverterCore"></typeparam>
/// <typeparam name="TSource"></typeparam>
/// <typeparam name="TDestination"></typeparam>
public abstract class UniDataTransformerMiddlewareBase<TConverterCore, TSource, TDestination> :
    DataTransformerMiddlewareBase,
    IDataUniConverterCore<TSource, TDestination>
    where TConverterCore : IDataUniConverterCore<TSource, TDestination>
{
    public override bool CanConvert(Type source)
    {
        return source.IsAssignableTo(typeof(TSource));
    }

    public override object? Convert(object? source)
    {
        if (source is TSource from) return Convert(from);
        throw new InvalidOperationException(
            $"Can not transform {source?.GetType()} to {typeof(TDestination)} in {nameof(TConverterCore)}");
    }

    public abstract TDestination Convert(TSource data);

}
/// <summary>
/// 双向数据协议转换中间件基类
/// </summary>
/// <typeparam name="TConverterCore"></typeparam>
/// <typeparam name="T1"></typeparam>
/// <typeparam name="T2"></typeparam>
public abstract class BiDataTransformerMiddlewareBase<TConverterCore, T1, T2> :
    DataTransformerMiddlewareBase,
    IDataBiConverterCore<T1, T2>
    where TConverterCore : IDataBiConverterCore<T1, T2>
{
    public override bool CanConvert(Type source)
    {
        return source.IsAssignableTo(typeof(T1)) || source.IsAssignableTo(typeof(T2));
    }
    public abstract T2 Convert(T1 data);
    public abstract T1 Convert(T2 data);
    public override object? Convert(object? source)
    {
        return source switch
        {
            T1 t1 => Convert(t1),
            T2 t2 => Convert(t2),
            _ => throw new InvalidOperationException(
                $"Can not transform {source?.GetType()} to {typeof(T1)} or {typeof(T2)} in {nameof(TConverterCore)}")
        };
    }
}