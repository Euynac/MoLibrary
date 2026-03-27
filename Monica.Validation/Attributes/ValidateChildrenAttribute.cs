using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Monica.Validation.Attributes;

/// <summary>
/// Represents a nested validation result.
/// </summary>
public class NestedValidationResult() : ValidationResult("")
{
    public IList<ValidationResult> NestedResults { get; set; } = [];
}

/// <summary>
/// Continues validation into nested object types, including both collections and regular objects.
/// </summary>
public class ValidateChildrenAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = new NestedValidationResult
        {
            ErrorMessage = $"Error occured at {validationContext.DisplayName}"
        };

        if (value == null)
        {
            //return ValidationResult.Success;
            return null;
        }

        if (value is not IEnumerable list)
        {
            // Single Object
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(value, validationContext, results, true);
            result.NestedResults = results;
            return result;
        }

        var recursiveResultList = new List<ValidationResult>();

        // List Object
        foreach (var item in list)
        {
            var nestedItemResult = new List<ValidationResult>();
            var context = new ValidationContext(item, validationContext, null);

            var nestedParentResult = new NestedValidationResult
            {
                ErrorMessage = $"Error occured at {validationContext.DisplayName}"
            };

            Validator.TryValidateObject(item, context, nestedItemResult, true);
            nestedParentResult.NestedResults = nestedItemResult;
            recursiveResultList.Add(nestedParentResult);
        }

        result.NestedResults = recursiveResultList;
        return result;
    }
}
