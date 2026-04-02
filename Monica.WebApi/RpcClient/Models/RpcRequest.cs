using System.ComponentModel.DataAnnotations;
using Monica.WebApi.Abstractions;
using Monica.WebApi.RpcClient.Abstractions;

namespace Monica.WebApi.RpcClient.Models;


public record RpcRequest : IValidatableObject, IHasRpcHttpInfo
{
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break;
    }

    public Dictionary<string, string?>? Headers { get; set; }
}

public record RpcRequest<TResponse> : RpcRequest, IResultRequest<TResponse>
{
    
}