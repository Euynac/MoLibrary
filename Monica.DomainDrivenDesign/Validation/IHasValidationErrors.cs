using System.ComponentModel.DataAnnotations;

namespace Monica.DomainDrivenDesign.Validation;

public interface IHasValidationErrors
{
    IList<ValidationResult> ValidationErrors { get; }
}
