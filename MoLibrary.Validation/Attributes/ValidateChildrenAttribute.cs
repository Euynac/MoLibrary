using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace MoLibrary.Validation.Attributes;

/// <summary>
/// 嵌套验证结果
/// </summary>
public class NestedValidationResult() : ValidationResult("")
{
    public IList<ValidationResult> NestedResults { get; set; } = [];
}

/// <summary>
/// 继续检查嵌套类类型，支持列表及普通类
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
