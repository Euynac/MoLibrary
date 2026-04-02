using System.ComponentModel.DataAnnotations;

namespace Monica.WebApi.Validation.Abstractions;

public interface IHasValidationErrors
{
    IList<ValidationResult> ValidationErrors { get; }
}
