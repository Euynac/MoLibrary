using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MoLibrary.Tool.General;

/// <summary>
/// https://stackoverflow.com/questions/1402803/passing-properties-by-reference-in-c-sharp
/// @Sven
/// </summary>
/// <typeparam name="T"></typeparam>
public class Accessor<T>
{
    private readonly Action<T> _setter;
    private readonly Func<T> _getter;

    public Accessor(Expression<Func<T>> expr)
    {
        var memberExpression = (MemberExpression)expr.Body;
        var instanceExpression = memberExpression.Expression;
        var parameter = Expression.Parameter(typeof(T));

        if (memberExpression.Member is PropertyInfo propertyInfo)
        {
            _setter = Expression.Lambda<Action<T>>(Expression.Call(instanceExpression, propertyInfo.GetSetMethod(), parameter), parameter).Compile();
            _getter = Expression.Lambda<Func<T>>(Expression.Call(instanceExpression, propertyInfo.GetGetMethod())).Compile();
        }
        else if (memberExpression.Member is FieldInfo fieldInfo)
        {
            _setter = Expression.Lambda<Action<T>>(Expression.Assign(memberExpression, parameter), parameter).Compile();
            _getter = Expression.Lambda<Func<T>>(Expression.Field(instanceExpression, fieldInfo)).Compile();
        }
        else
        {
            throw new Exception($"Can not create accessor for {expr}");
        }

    }

    public void Set(T value) => _setter(value);

    public T Get() => _getter();
}