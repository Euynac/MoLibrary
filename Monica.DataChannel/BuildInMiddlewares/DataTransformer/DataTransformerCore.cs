using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// Base class for data transformation middleware.
/// </summary>
public abstract class DataTransformerMiddlewareBase : PipeTransformMiddlewareBase, IDataConverterCore
{
    public override DataContext Pass(DataContext context)
    {
        if (context.Data == null) return context;
        if (context.DataType is { } type && CanConvert(type) && context.Data is { } data)
        {
            var convertedData = Convert(data);
            if (convertedData == null)
            {
                // TODO: Consider returning the reason for conversion failure.
                return context;
            }
            context.Data = convertedData;
        }

        return context;
    }

    public abstract bool CanConvert(Type source);
    public abstract object? Convert(object? source);
}

/// <summary>
/// Base class for unidirectional data transformation middleware.
/// </summary>
/// <typeparam name="TConverterCore">The converter core type.</typeparam>
/// <typeparam name="TSource">The source payload type.</typeparam>
/// <typeparam name="TDestination">The destination payload type.</typeparam>
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
            $"Cannot transform {source?.GetType()} to {typeof(TDestination)} in {nameof(TConverterCore)}");
    }

    public abstract TDestination Convert(TSource data);

}
/// <summary>
/// Base class for bidirectional data transformation middleware.
/// </summary>
/// <typeparam name="TConverterCore">The converter core type.</typeparam>
/// <typeparam name="T1">The first supported payload type.</typeparam>
/// <typeparam name="T2">The second supported payload type.</typeparam>
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
                $"Cannot transform {source?.GetType()} to {typeof(T1)} or {typeof(T2)} in {nameof(TConverterCore)}")
        };
    }
}
