using System;
using System.Dynamic;
using System.Net;

namespace MoLibrary.Tool.MoResponse;

/// <summary>
/// 响应类不要直接使用该接口实现，而是继承ServiceResponse。
/// </summary>
public interface IServiceResponse
{
    /// <summary>
    /// 响应信息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 响应码
    /// </summary>
    public ResponseCode? Code { get; set; }

    /// <summary>
    /// 接口扩展Debug信息，如调用链信息等
    /// </summary>
    public ExpandoObject? ExtraInfo { get; set; }

    /// <summary>
    /// 获取响应码对应的HttpStatusCode
    /// </summary>
    /// <returns></returns>
    public HttpStatusCode? GetHttpStatusCode()
    {
        switch (Code)
        {
            case ResponseCode.Ok:
                return HttpStatusCode.OK;


            case ResponseCode.Unauthorized:
            case ResponseCode.RefreshTokenExpired:
            case ResponseCode.AccessTokenExpired:
                return HttpStatusCode.Unauthorized;


            case ResponseCode.Forbidden:
                return HttpStatusCode.Forbidden;


            case ResponseCode.ValidateError:
            case ResponseCode.ErrorWarning:
            case ResponseCode.BadRequest:
                return HttpStatusCode.BadRequest;


            case ResponseCode.InternalError:
                return HttpStatusCode.InternalServerError;


            case null:
            case ResponseCode.Unknown:
                return null;
            default:
                throw new ArgumentOutOfRangeException(Code.ToString(), $"未填写当前状态码{Code}对应HTTP状态码的值！");
        }
    }
}