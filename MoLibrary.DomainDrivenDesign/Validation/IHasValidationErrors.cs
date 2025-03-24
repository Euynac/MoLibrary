using System.ComponentModel.DataAnnotations;

namespace MoLibrary.DomainDrivenDesign.Validation;

public interface IHasValidationErrors
{
    IList<ValidationResult> ValidationErrors { get; }
}
