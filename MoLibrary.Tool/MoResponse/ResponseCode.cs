namespace MoLibrary.Tool.MoResponse;

/// <summary>
/// 通用返回码
/// </summary>
public enum ResponseCode
{
    Unknown = 0,
    /// <summary>
    /// 请求正常
    /// </summary>
    Ok = 200,
    /// <summary>
    /// 请求错误
    /// </summary>
    BadRequest = 400,
    /// <summary>
    /// 未登录
    /// </summary>
    Unauthorized = 401,
    /// <summary>
    /// 刷新Token失效
    /// </summary>
    RefreshTokenExpired = 452,
    /// <summary>
    /// 访问Token失效
    /// </summary>
    AccessTokenExpired = 453,

    /// <summary>
    /// 警告错误，一般需用户确认
    /// </summary>
    ErrorWarning = 460,
    /// <summary>
    /// 权限不足
    /// </summary>
    Forbidden = 403,
    /// <summary>
    /// 输入验证错误
    /// </summary>
    ValidateError = 451,
    /// <summary>
    /// 系统异常
    /// </summary>
    InternalError = 500,
}