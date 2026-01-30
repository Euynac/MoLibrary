using System.ComponentModel.DataAnnotations;
using Monica.DomainDrivenDesign.Interfaces;

namespace Monica.DomainDrivenDesign.AutoController.MoRpc;


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