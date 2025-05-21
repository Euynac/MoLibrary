using System.ComponentModel.DataAnnotations;
using MoLibrary.DomainDrivenDesign.Interfaces;

namespace MoLibrary.Framework.Features.MoRpc;


public record MoRpcRequest : IValidatableObject, IHasRpcHttpInfo
{
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break;
    }

    public Dictionary<string, string?>? Headers { get; set; }
}

public record MoRpcRequest<TResponse> : MoRpcRequest, IMoRequest<TResponse>
{
    
}