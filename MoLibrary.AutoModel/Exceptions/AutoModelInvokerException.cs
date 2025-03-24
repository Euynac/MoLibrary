namespace MoLibrary.AutoModel.Exceptions;

/// <summary>
/// 调用执行错误
/// </summary>
/// <param name="message"></param>
public class AutoModelInvokerException(string message) : AutoModelBaseException(message);