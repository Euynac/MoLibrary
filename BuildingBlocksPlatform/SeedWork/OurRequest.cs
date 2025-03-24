using BuildingBlocksPlatform.Features.GRPCExtensions;
using MediatR;
using System.ComponentModel.DataAnnotations;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.SeedWork;


public record OurRequest : IValidatableObject, IHasGrpcHttpInfo
{
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break;
    }

    public Dictionary<string, string?>? Headers { get; set; }
}

public record OurRequest<TResponse> : OurRequest, IRequest<Res<TResponse>>
{
    
}