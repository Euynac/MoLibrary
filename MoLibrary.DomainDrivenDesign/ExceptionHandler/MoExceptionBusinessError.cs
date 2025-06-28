using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

/// <summary>
/// 业务异常，一般用于非<see cref="Res"/>类型返回值
/// </summary>
/// TODO 禁用堆栈
public class MoExceptionBusinessError(string? message) : Exception(message)
{
    
}