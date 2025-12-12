using System.Reflection;
using System.Text;
using MoLibrary.EventBus.Constants;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Helpers;

/// <summary>
/// 委托元数据提取器，用于从委托中提取方法信息
/// </summary>
internal static class DelegateMetadataExtractor
{
    /// <summary>
    /// 从委托中提取元数据
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理委托</param>
    /// <returns>包含元数据的字典</returns>
    public static Dictionary<string, object> ExtractMetadata<TEvent>(Func<TEvent, Task> handler)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            var method = handler.Method;

            // 提取方法名称
            metadata[SubscriptionMetadataKeys.ActionMethodName] = method.Name;

            // 提取声明类型
            if (method.DeclaringType != null)
            {
                metadata[SubscriptionMetadataKeys.ActionDeclaringType] =
                    method.DeclaringType.FullName ?? method.DeclaringType.Name;
            }

            // 构建方法签名
            var signature = BuildMethodSignature(method);
            metadata[SubscriptionMetadataKeys.ActionMethodSignature] = signature;

            // 标识是否为静态方法
            metadata[SubscriptionMetadataKeys.ActionIsStatic] = method.IsStatic;
        }
        catch (Exception)
        {
            // 如果提取失败，返回部分元数据
            // 这确保元数据提取失败不会影响订阅注册
        }

        return metadata;
    }

    /// <summary>
    /// 构建人类可读的方法签名
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>方法签名字符串</returns>
    private static string BuildMethodSignature(MethodInfo method)
    {
        var sb = new StringBuilder();

        // 返回类型
        sb.Append(method.ReturnType.GetCleanName());
        sb.Append(' ');

        // 方法名
        sb.Append(method.Name);
        sb.Append('(');

        // 参数
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(parameters[i].ParameterType.GetCleanName());
            sb.Append(' ');
            sb.Append(parameters[i].Name);
        }

        sb.Append(')');

        return sb.ToString();
    }

   
}
