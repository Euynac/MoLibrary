namespace MoLibrary.AutoModel.Exceptions;

/// <summary>
/// 值类型转换错误
/// </summary>
public class AutoModelValueConvertException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);