using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MoLibrary.Authority.Implements.Authorization;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.DomainDrivenDesign.Validation;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

internal class MoExceptionHandler(ILogger<MoExceptionHandler> logger, IHttpContextAccessor accessor) : IMoExceptionHandler
{
    public Task<Res> TryHandleWithCurrentHttpContextAsync(Exception exception, CancellationToken cancellationToken)
    {
        return TryHandleAsync(accessor.HttpContext, exception, cancellationToken);
    }

    public void LogException(HttpContext? httpContext, Exception exception)
    {
        logger.LogError(exception, $"{httpContext?.Request.Path} threw an exception");
    }
    public async Task<Res> TryHandleAsync(HttpContext? httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetail = new ProblemDetails();
        switch (exception)
        {
            case MoExceptionBusinessError businessError:
                return Res.Fail(businessError.Message);
            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.NotLogin }:
                return MoAuthorizationRes.NotLogin();

            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.RefreshTokenExpired }:
                return MoAuthorizationRes.RefreshTokenExpired();

            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.AccessTokenExpired } e:
                return MoAuthorizationRes.AccessTokenExpired(e.Reason);
            case SecurityTokenExpiredException expired:
                return MoAuthorizationRes.AccessTokenExpired(expired.Message);
            case MoAuthorizationException authorizationException:
            {
                problemDetail.Title = authorizationException.Reason;
                return new ResError<ProblemDetails>(problemDetail, authorizationException.Title, ResponseCode.Forbidden);
            }
            case SecurityTokenArgumentException tokenMalformedException:
                return new Res("用户Token异常", ResponseCode.Unauthorized).AppendExtraInfo("detail",
                    tokenMalformedException.Message);

            case SecurityTokenException:
                return new Res("用户Token异常", ResponseCode.Unauthorized).AppendExtraInfo("detail",
                    exception.Message);
     
            case MoValidationException validationException:
                return Res.CreateError(validationException.ValidationErrors, "接口请求参数校验失败",
                    ResponseCode.ValidateError);

            default:
            {
                problemDetail = new ProblemDetails
                {
                    //StatusCodes.Status500InternalServerError
                    Status = httpContext?.Response.StatusCode,
                    Title = exception.GetMessageRecursively(),
                    Extensions =
                    {
                        ["stackTrace"] = exception.ToString().Split(new[]{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries),
                        ["response"] = httpContext?.Response.CloneAs<DtoHttpContextResponse>(),
                        ["request"] = httpContext?.Request.CloneAs<DtoHttpContextRequest>(),
                        ["connection"] = httpContext?.Connection.ToJsonStringForce(),
                        ["chainInfo"] = httpContext?.GetOrDefault<MoRequestContext>(),
                        ["time"] = DateTime.Now,
                        ["utcTime"] = DateTime.UtcNow,
                    }
                };

                return new ResError<ProblemDetails>(problemDetail, "服务器出现异常", ResponseCode.InternalError);
            }
        }
    }
}



file class DtoConnectionInfo
{
    /// <summary>
    /// Gets or sets a unique identifier to represent this connection.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the remote target. Can be null.
    /// </summary>
    /// <remarks>
    /// The result is <c>null</c> if the connection isn't a TCP connection, e.g., a Unix Domain Socket or a transport that isn't TCP based.
    /// </remarks>
    public IPAddress? RemoteIpAddress { get; set; }

    /// <summary>Gets or sets the port of the remote target.</summary>
    public int RemotePort { get; set; }

    /// <summary>Gets or sets the IP address of the local host.</summary>
    public IPAddress? LocalIpAddress { get; set; }

    /// <summary>Gets or sets the port of the local host.</summary>
    public int LocalPort { get; set; }
}

file class DtoHttpContextRequest
{
    /// <summary>Gets or sets the HTTP method.</summary>
    /// <returns>The HTTP method.</returns>
    public string Method { get; set; }

    /// <summary>Gets or sets the HTTP request scheme.</summary>
    /// <returns>The HTTP request scheme.</returns>
    public string Scheme { get; set; }

    /// <summary>Returns true if the RequestScheme is https.</summary>
    /// <returns>true if this request is using https; otherwise, false.</returns>
    public bool IsHttps { get; set; }

    /// <summary>Gets or sets the Host header. May include the port.</summary>
    /// <return>The Host header.</return>
    public HostString Host { get; set; }

    /// <summary>
    /// Gets or sets the base path for the request. The path base should not end with a trailing slash.
    /// </summary>
    /// <returns>The base path for the request.</returns>
    public PathString PathBase { get; set; }

    /// <summary>
    /// Gets or sets the portion of the request path that identifies the requested resource.
    /// <para>
    /// The value may be <see cref="F:Microsoft.AspNetCore.Http.PathString.Empty" /> if <see cref="P:Microsoft.AspNetCore.Http.HttpRequest.PathBase" /> contains the full path,
    /// or for 'OPTIONS *' requests.
    /// The path is fully decoded by the server except for '%2F', which would decode to '/' and
    /// change the meaning of the path segments. '%2F' can only be replaced after splitting the path into segments.
    /// </para>
    /// </summary>
    public PathString? Path { get; set; }

    /// <summary>
    /// Gets or sets the raw query string used to create the query collection in Request.Query.
    /// </summary>
    /// <returns>The raw query string.</returns>
    public QueryString? QueryString { get; set; }

    /// <summary>
    /// Gets the query value collection parsed from Request.QueryString.
    /// </summary>
    /// <returns>The query value collection parsed from Request.QueryString.</returns>
    public IQueryCollection? Query { get; set; }

    /// <summary>Gets or sets the request protocol (e.g. HTTP/1.1).</summary>
    /// <returns>The request protocol.</returns>
    public string? Protocol { get; set; }

    /// <summary>Gets the request headers.</summary>
    /// <returns>The request headers.</returns>
    public IHeaderDictionary? Headers { get; }

    /// <summary>Gets the collection of Cookies for this request.</summary>
    /// <returns>The collection of Cookies for this request.</returns>
    public IRequestCookieCollection? Cookies { get; set; }

    /// <summary>Gets or sets the Content-Length header.</summary>
    /// <returns>The value of the Content-Length header, if any.</returns>
    public long? ContentLength { get; set; }

    /// <summary>Gets or sets the Content-Type header.</summary>
    /// <returns>The Content-Type header.</returns>
    public string? ContentType { get; set; }

    ///// <summary>
    ///// Gets or sets the request body <see cref="T:System.IO.Stream" />.
    ///// </summary>
    ///// <value>The request body <see cref="T:System.IO.Stream" />.</value>
    //public Stream Body { get; set; }

    /// <summary>Gets the collection of route values for this request.</summary>
    /// <returns>The collection of route values for this request.</returns>
    public RouteValueDictionary? RouteValues { get; set; }
}

file class DtoHttpContextResponse
{
    /// <summary>Gets or sets the HTTP response code.</summary>
    public int StatusCode { get; set; }

    /// <summary>Gets the response headers.</summary>
    public IHeaderDictionary Headers { get; }

    ///// <summary>
    ///// Gets or sets the response body <see cref="T:System.IO.Stream" />.
    ///// </summary>
    //public Stream Body { get; set; }

    /// <summary>
    /// Gets or sets the value for the <c>Content-Length</c> response header.
    /// </summary>
    public long? ContentLength { get; set; }

    /// <summary>
    /// Gets or sets the value for the <c>Content-Type</c> response header.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets an object that can be used to manage cookies for this response.
    /// </summary>
    public IResponseCookies Cookies { get; }
}