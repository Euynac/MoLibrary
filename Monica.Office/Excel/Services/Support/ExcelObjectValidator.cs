using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Validates row objects created during Excel import.
    /// </summary>
    internal static class ExcelObjectValidator
    {
        /// <summary>
        /// Gets validation results for the provided object.
        /// </summary>
        /// <param name="instance">The object instance to validate.</param>
        /// <returns>Returns <see langword="null"/> when there are no validation errors.</returns>
        public static List<ValidationResult>? GetValidationResults(object? instance)
        {
            if (instance == null)
            {
                return null;
            }

            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults, true);
            return isValid ? null : validationResults;
        }
    }
}
