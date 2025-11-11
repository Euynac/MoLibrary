using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MoLibrary.Core.ExceptionHandler;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority.Implements.Authorization;

internal class MoAuthorizationExceptionHandler : IMoExceptionHandlerPack
{
    public bool TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? res)
    {
        switch (exception)
        {
            case MoExceptionBusinessError businessError:
                res = Res.Fail(businessError.Message);
                return true;

            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.NotLogin }:
                res = MoAuthorizationRes.NotLogin();
                return true;

            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.RefreshTokenExpired }:
                res = MoAuthorizationRes.RefreshTokenExpired();
                return true;

            case MoAuthorizationException { Type: MoAuthorizationException.ExceptionType.AccessTokenExpired } e:
                res = MoAuthorizationRes.AccessTokenExpired(e.Reason);
                return true;

            case SecurityTokenExpiredException expired:
                res = MoAuthorizationRes.AccessTokenExpired(expired.Message);
                return true;

            case MoAuthorizationException authorizationException:
            {
                var problemDetail = new ProblemDetails { Title = authorizationException.Reason };
                res = new ResError<ProblemDetails>(problemDetail, authorizationException.Title, ResponseCode.Forbidden);
                return true;
            }

            case SecurityTokenArgumentException tokenMalformedException:
                res = new Res("用户Token异常", ResponseCode.Unauthorized).AppendExtraInfo("detail",
                    tokenMalformedException.Message);
                return true;

            case SecurityTokenException:
                res = new Res("用户Token异常", ResponseCode.Unauthorized).AppendExtraInfo("detail",
                    exception.Message);
                return true;

            default:
                res = null;
                return false;
        }
    }
}

