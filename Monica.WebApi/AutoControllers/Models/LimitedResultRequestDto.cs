using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Simply implements <see cref="IHasRequestLimitedResult" />.
/// </summary>
[Serializable]
public class LimitedResultRequestDto : IHasRequestLimitedResult, IValidatableObject
{
    private int? _maxResultCount;

    /// <summary>
    /// Maximum result count should be returned.
    /// This is generally used to limit result count on paging. When omitted from an HTTP request,
    /// the value is populated from the current host's AutoController paging options during validation.
    /// </summary>
    [Range(1, 2147483647)]
    public virtual int MaxResultCount
    {
        get => _maxResultCount ?? AutoControllerPaginationDefaults.DefaultResultCount;
        set => _maxResultCount = value;
    }

    /// <inheritdoc />
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var pagination = (validationContext.GetService(typeof(IOptions<CrudControllerOption>)) as
            IOptions<CrudControllerOption>)?.Value.Pagination ?? new AutoControllerPaginationOption();

        ApplyDefaults(pagination);

        var maximumResultCount = GetMaximumResultCount(pagination);
        if (MaxResultCount > maximumResultCount)
        {
            yield return new ValidationResult(
                $"{nameof(MaxResultCount)} exceeds the limit of {maximumResultCount}.",
                [nameof(MaxResultCount)]);
        }
    }

    /// <summary>
    /// Resolves the host-specific maximum for this request type.
    /// </summary>
    /// <param name="pagination">The current host's pagination options.</param>
    /// <returns>The largest accepted result count.</returns>
    protected virtual int GetMaximumResultCount(AutoControllerPaginationOption pagination)
    {
        return pagination.MaximumResultCount;
    }

    internal void ApplyDefaults(AutoControllerPaginationOption pagination)
    {
        _maxResultCount ??= pagination.DefaultResultCount;
    }
}
