namespace BuildingBlocksPlatform.AutoModel.Exceptions;

/// <summary>
/// 调用执行错误
/// </summary>
/// <param name="message"></param>
public class AutoModelBaseException(string message) : Exception(message);