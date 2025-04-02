using MediatR;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.Decorators;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("----- Handling command {CommandName} ({@Command})", request.GetCleanFullName(),
            request);
        var response = await next();
        logger.LogInformation("----- Command {CommandName} handled - response: {@Response}",
            request.GetCleanFullName(), response);

        return response;
    }
}