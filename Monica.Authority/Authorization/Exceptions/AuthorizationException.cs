using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Monica.Authority.Localization;
using Monica.Tool.Extensions;

namespace Monica.Authority.Authorization.Exceptions;

public class AuthorizationException : Exception
{
    public AuthorizationFailure? Failure { get; }
    public Exception? FailureException { get; }

    public string? Reason { get; set; }

    public ExceptionType Type { get; set; }
    public enum ExceptionType
    {
        Unknown = 0,
        PermissionDenied,
        NotLogin,
        AccessTokenExpired,
        RefreshTokenExpired,
        TokenException
    }

    public AuthorizationException(ExceptionType type) : base($"Authorization error: {type}")
    {
        Type = type;
    }

    public AuthorizationException(AuthorizationFailure? failure) : base("Authorization failed")
    {
        Type = ExceptionType.PermissionDenied;
        Failure = failure;
        if (Failure != null)
        {
            Reason = Failure.FailureReasons.Select(p => p.Message).StringJoin(",");
            Reason = Failure.FailedRequirements.Select(p => p.ToString()).StringJoin(",") is { } requirements &&
                     !string.IsNullOrWhiteSpace(requirements)
                ? $"{Reason.BeIfNotWhiteSpace($"{Reason};")}Requirements:{requirements}"
                : Reason;
            if (Failure.FailedRequirements.Any(p => p is DenyAnonymousAuthorizationRequirement))
            {
                Type = ExceptionType.NotLogin;
            }
        }

    }
    public AuthorizationException(Exception? failure) : base($"Authorization failed: {failure?.Message}")
    {
        FailureException = failure;
        if (failure != null)
        {
            switch (failure)
            {
                case SecurityTokenExpiredException expired:
                    Reason = expired.Message;
                    Type = ExceptionType.AccessTokenExpired;
                    break;
                case SecurityTokenArgumentException:
                case SecurityTokenException:
                    Reason = $"Token exception: [{failure.GetType()}]{failure.Message}";
                    Type = ExceptionType.TokenException;
                    break;
                default:
                    Reason = $"[{failure.GetType().Name}]{failure.Message}";
                    break;
            }
        }
    }

    public string GetTitle(AuthorityMessageLocalizer localizer)
    {
        return localizer.GetAuthorizationExceptionTitle(Type);
    }
}
