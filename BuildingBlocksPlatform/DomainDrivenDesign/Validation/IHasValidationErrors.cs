using System.ComponentModel.DataAnnotations;

namespace BuildingBlocksPlatform.DomainDrivenDesign.Validation;

public interface IHasValidationErrors
{
    IList<ValidationResult> ValidationErrors { get; }
}
