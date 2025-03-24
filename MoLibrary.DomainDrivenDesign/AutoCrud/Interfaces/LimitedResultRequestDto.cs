using System.ComponentModel.DataAnnotations;

namespace MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// Simply implements <see cref="IHasRequestLimitedResult" />.
/// </summary>
[Serializable]
public class LimitedResultRequestDto : IHasRequestLimitedResult, IValidatableObject
{
    /// <summary>Default value: 10.</summary>
    public static int DefaultMaxResultCount { get; set; } = 10;

    /// <summary>
    /// Maximum possible value of the <see cref="MaxResultCount" />.
    /// Default value: 1,000.
    /// </summary>
    public static int MaxMaxResultCount { get; set; } = 1000;

    /// <summary>
    /// Maximum result count should be returned.
    /// This is generally used to limit result count on paging.
    /// </summary>
    [Range(1, 2147483647)]
    public virtual int MaxResultCount { get; set; } = DefaultMaxResultCount;

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxResultCount > MaxMaxResultCount)
            yield return new ValidationResult($"{nameof(MaxResultCount)}超过{MaxMaxResultCount}限制", [nameof(MaxResultCount)]);
        //yield return new ValidationResult((string) validationContext.GetRequiredService<IStringLocalizer<AbpDddApplicationContractsResource>>()["MaxResultCountExceededExceptionMessage", new object[4]
        //    {
        //        (object) "MaxResultCount",
        //        (object) LimitedResultRequestDto.MaxMaxResultCount,
        //        (object) typeof (LimitedResultRequestDto).FullName,
        //        (object) "MaxMaxResultCount"
        //    }], (IEnumerable<string>) new string[1]
        //    {
        //        "MaxResultCount"
        //    });
    }
}