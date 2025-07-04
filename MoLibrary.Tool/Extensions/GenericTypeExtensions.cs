using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoLibrary.Tool.Extensions;


public static class GenericTypeExtensions
{

    /// <summary>
    /// 获取类型的完整命名空间路径名称，处理泛型、嵌套类和匿名类型，一般用于日志格式化
    /// </summary>
    public static string GetCleanFullName(this Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        return InnerGetCleanName(type);
        string InnerGetCleanName(Type curType, bool jumpDeclareType = false, bool getFullName = true)
        {
            // 处理嵌套类（优先级最高） 类型名称中的 + 符号表示 ​嵌套类（Nested Class）​。
            if (curType.DeclaringType != null && !jumpDeclareType)
            {
                var parentName = curType.DeclaringType.GetCleanFullName();
                var currentName = InnerGetCleanName(curType, true, false);
                return $"{parentName}.{currentName}";
            }

            // 处理匿名类型
            if (curType.Name.Contains("AnonymousType", StringComparison.Ordinal))
            {
                var signature = string.Join("-", curType.GetProperties()
                    .Select(p => $"{p.Name}[{p.PropertyType.GetCleanFullName()}]"));
                return $"{curType.Assembly.GetName().Name}:AnonymousType_{signature}";
            }

            // 处理泛型类型
            if (curType.IsGenericType)
            {
                var genericTypeDef = curType.GetGenericTypeDefinition();
                var genericArgs = curType.GetGenericArguments().Select(t => InnerGetCleanName(t, true)).ToArray();

                // 构造完整类型名称
                var typeName = getFullName ? genericTypeDef.FullName ?? genericTypeDef.Name : genericTypeDef.Name;
                var index = typeName.IndexOf('`');
                if (index != -1) typeName = typeName[..index];
                return typeName + $"<{string.Join(",", genericArgs)}>";
            }

            return getFullName ? curType.FullName ?? curType.Name : curType.Name;
        }
    }

    public static string GetCleanFullName(this object @object)
    {
        return @object.GetType().GetCleanFullName();
    }

    public static bool IsCollectionType(this Type type)
    {
        return type.IsGenericType && type.GetInterfaces()
            .Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    public static bool IsDictionary(this Type type)
    {
        //or typeof(IDictionary).IsAssignableFrom(type);
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
    }

    /// <summary>
    /// Get the underlying type of generic T in such as type of IEnumerable&lt;T&gt; and T?.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type GetGenericUnderlyingType(this Type type)
    {
        Type? underlyingType = null;
        if (!type.IsGenericType) return type;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (type.IsArray)
            {
                underlyingType = type.GetElementType() ?? throw new Exception($"{type.FullName} not support in method {nameof(GetGenericUnderlyingType)}");
            }
            else
            {
                underlyingType = type.GetGenericArguments().FirstOrDefault() ?? throw new Exception($"{type.FullName} not support in method {nameof(GetGenericUnderlyingType)}");
            }
                
        }

        underlyingType ??= type;
        if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof (Nullable<>))
        {
            underlyingType = Nullable.GetUnderlyingType(type);
        }

        return underlyingType ?? type;
    }
}


#region ASP NET Core

//// Copyright(c) .NET Foundation and Contributors
////
//// All rights reserved.
////
//// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
//// this file except in compliance with the License. You may obtain a copy of the
//// License at
////
//// http://www.apache.org/licenses/LICENSE-2.0
////
//// Unless required by applicable law or agreed to in writing, software distributed
//// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
//// CONDITIONS OF ANY KIND, either express or implied. See the License for the
//// specific language governing permissions and limitations under the License.
////
//// From https://github.com/dotnet/aspnetcore/blob/b1dcacabec1aeacef72c9aa2909f1cb49993fa73/src/Shared/TypeNameHelper/TypeNameHelper.cs

//// ReSharper disable All

//using System;
//using System.Text;
//using System.Collections.Generic;

//namespace Microsoft.Extensions.Internal
//{
//    internal static class TypeNameHelper
//    {
//        private const char DefaultNestedTypeDelimiter = '+';

//        private static readonly Dictionary<Type, string> _builtInTypeNames = new Dictionary<Type, string>
//        {
//            { typeof(void), "void" },
//            { typeof(bool), "bool" },
//            { typeof(byte), "byte" },
//            { typeof(char), "char" },
//            { typeof(decimal), "decimal" },
//            { typeof(double), "double" },
//            { typeof(float), "float" },
//            { typeof(int), "int" },
//            { typeof(long), "long" },
//            { typeof(object), "object" },
//            { typeof(sbyte), "sbyte" },
//            { typeof(short), "short" },
//            { typeof(string), "string" },
//            { typeof(uint), "uint" },
//            { typeof(ulong), "ulong" },
//            { typeof(ushort), "ushort" }
//        };

//        public static string? GetTypeDisplayName(object item, bool fullName = true)
//        {
//            return item == null ? null : GetTypeDisplayName(item.GetType(), fullName);
//        }

//        /// <summary>
//        /// Pretty print a type name.
//        /// </summary>
//        /// <param name="type">The <see cref="Type"/>.</param>
//        /// <param name="fullName"><c>true</c> to print a fully qualified name.</param>
//        /// <param name="includeGenericParameterNames"><c>true</c> to include generic parameter names.</param>
//        /// <param name="includeGenericParameters"><c>true</c> to include generic parameters.</param>
//        /// <param name="nestedTypeDelimiter">Character to use as a delimiter in nested type names</param>
//        /// <returns>The pretty printed type name.</returns>
//        public static string GetTypeDisplayName(Type type, bool fullName = true, bool includeGenericParameterNames = false, bool includeGenericParameters = true, char nestedTypeDelimiter = DefaultNestedTypeDelimiter)
//        {
//            var builder = new StringBuilder();
//            ProcessType(builder, type, new DisplayNameOptions(fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter));
//            return builder.ToString();
//        }

//        private static void ProcessType(StringBuilder builder, Type type, in DisplayNameOptions options)
//        {
//            if (type.IsGenericType)
//            {
//                var genericArguments = type.GetGenericArguments();
//                ProcessGenericType(builder, type, genericArguments, genericArguments.Length, options);
//            }
//            else if (type.IsArray)
//            {
//                ProcessArrayType(builder, type, options);
//            }
//            else if (_builtInTypeNames.TryGetValue(type, out var builtInName))
//            {
//                builder.Append(builtInName);
//            }
//            else if (type.IsGenericParameter)
//            {
//                if (options.IncludeGenericParameterNames)
//                {
//                    builder.Append(type.Name);
//                }
//            }
//            else
//            {
//                var name = options.FullName ? type.FullName! : type.Name;
//                builder.Append(name);

//                if (options.NestedTypeDelimiter != DefaultNestedTypeDelimiter)
//                {
//                    builder.Replace(DefaultNestedTypeDelimiter, options.NestedTypeDelimiter, builder.Length - name.Length, name.Length);
//                }
//            }
//        }

//        private static void ProcessArrayType(StringBuilder builder, Type type, in DisplayNameOptions options)
//        {
//            var innerType = type;
//            while (innerType.IsArray)
//            {
//                innerType = innerType.GetElementType()!;
//            }

//            ProcessType(builder, innerType, options);

//            while (type.IsArray)
//            {
//                builder.Append('[');
//                builder.Append(',', type.GetArrayRank() - 1);
//                builder.Append(']');
//                type = type.GetElementType()!;
//            }
//        }

//        private static void ProcessGenericType(StringBuilder builder, Type type, Type[] genericArguments, int length, in DisplayNameOptions options)
//        {
//            var offset = 0;
//            if (type.IsNested)
//            {
//                offset = type.DeclaringType!.GetGenericArguments().Length;
//            }

//            if (options.FullName)
//            {
//                if (type.IsNested)
//                {
//                    ProcessGenericType(builder, type.DeclaringType!, genericArguments, offset, options);
//                    builder.Append(options.NestedTypeDelimiter);
//                }
//                else if (!string.IsNullOrEmpty(type.Namespace))
//                {
//                    builder.Append(type.Namespace);
//                    builder.Append('.');
//                }
//            }

//            var genericPartIndex = type.Name.IndexOf('`');
//            if (genericPartIndex <= 0)
//            {
//                builder.Append(type.Name);
//                return;
//            }

//            builder.Append(type.Name, 0, genericPartIndex);

//            if (options.IncludeGenericParameters)
//            {
//                builder.Append('<');
//                for (var i = offset; i < length; i++)
//                {
//                    ProcessType(builder, genericArguments[i], options);
//                    if (i + 1 == length)
//                    {
//                        continue;
//                    }

//                    builder.Append(',');
//                    if (options.IncludeGenericParameterNames || !genericArguments[i + 1].IsGenericParameter)
//                    {
//                        builder.Append(' ');
//                    }
//                }
//                builder.Append('>');
//            }
//        }

//        private readonly struct DisplayNameOptions
//        {
//            public DisplayNameOptions(bool fullName, bool includeGenericParameterNames, bool includeGenericParameters, char nestedTypeDelimiter)
//            {
//                FullName = fullName;
//                IncludeGenericParameters = includeGenericParameters;
//                IncludeGenericParameterNames = includeGenericParameterNames;
//                NestedTypeDelimiter = nestedTypeDelimiter;
//            }

//            public bool FullName { get; }

//            public bool IncludeGenericParameters { get; }

//            public bool IncludeGenericParameterNames { get; }

//            public char NestedTypeDelimiter { get; }
//        }
//    }
//}


#endregion