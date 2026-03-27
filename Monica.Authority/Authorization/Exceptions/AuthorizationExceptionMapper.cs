using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Monica.Authority.Localization;
using Monica.Authority.Authorization.Services.Support;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.ExceptionHandling.Exceptions;
using Monica.Tool.Results;

namespace Monica.Authority.Authorization.Exceptions;

internal class AuthorizationExceptionMapper(AuthorityMessageLocalizer authorityLocalizer) : IExceptionResponseMapper
{
    public bool TryMap(
        HttpContext? httpContext,
        Exception exception,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? response)
    {
        switch (exception)
        {
            case BusinessException businessError:
                response = Res.Fail(businessError.Message);
                return true;

            case AuthorizationException { Type: AuthorizationException.ExceptionType.NotLogin }:
                response = ResultsAuthorization.NotLogin(authorityLocalizer);
                return true;

            case AuthorizationException { Type: AuthorizationException.ExceptionType.RefreshTokenExpired }:
                response = ResultsAuthorization.RefreshTokenExpired(authorityLocalizer);
                return true;

            case AuthorizationException { Type: AuthorizationException.ExceptionType.AccessTokenExpired } e:
                response = ResultsAuthorization.AccessTokenExpired(authorityLocalizer, e.Reason);
                return true;

            case SecurityTokenExpiredException expired:
                response = ResultsAuthorization.AccessTokenExpired(authorityLocalizer, expired.Message);
                return true;

            case AuthorizationException authorizationException:
            {
                var problemDetail = new ProblemDetails { Title = authorizationException.Reason };
                response = Res.Fail(
                        authorizationException.GetTitle(authorityLocalizer),
                        ResStatus.Forbidden)
                    .AppendMetadata("error", problemDetail);
                return true;
            }

            case SecurityTokenArgumentException tokenMalformedException:
                response = new Res(authorityLocalizer.GetTokenExceptionMessage(), ResStatus.Unauthorized).AppendMetadata("detail",
                    tokenMalformedException.Message);
                return true;

            case SecurityTokenException:
                response = new Res(authorityLocalizer.GetTokenExceptionMessage(), ResStatus.Unauthorized).AppendMetadata("detail",
                    exception.Message);
                return true;

            default:
                response = null;
                return false;
        }
    }
}
