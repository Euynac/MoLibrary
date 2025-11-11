namespace MoLibrary.AutoModel.Exceptions;

/// <summary>
/// 表达式/查询标准化/解析错误
/// </summary>
public class AutoModelNormalizeException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Token 表达式生成错误
/// </summary>
public class AutoModelTokenExpGenException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);