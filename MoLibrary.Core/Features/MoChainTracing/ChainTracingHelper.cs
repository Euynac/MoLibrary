using Microsoft.AspNetCore.Mvc;

namespace MoLibrary.Core.Features.MoChainTracing;

public static class ChainTracingHelper
{
    /// <summary>
    /// 获取响应类型名称
    /// </summary>
    /// <param name="type">返回类型</param>
    /// <returns>响应类型名称</returns>
    public static string GetResponseTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericArg = type.GenericTypeArguments.FirstOrDefault();
            while (genericArg?.IsGenericType == true)
            {
                genericArg = genericArg.GenericTypeArguments.FirstOrDefault();
            }
            return genericArg?.Name ?? type.Name;
        }
        return type.Name;
    }
    /// <summary>
    /// 从 ActionResult 中提取实际的结果对象
    /// </summary>
    /// <param name="result">Action 结果</param>
    /// <returns>实际的结果对象</returns>
    public static object? ExtractResult(IActionResult? result)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.Value,
            JsonResult jsonResult => jsonResult.Value,
            ContentResult contentResult => contentResult.Content,
            _ => result
        };
    }
}