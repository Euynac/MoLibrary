using Microsoft.Extensions.Options;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Services.Support;

internal sealed class CrudControllerOptionValidator : IValidateOptions<CrudControllerOption>
{
    public ValidateOptionsResult Validate(string? name, CrudControllerOption options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.RoutePath))
        {
            failures.Add($"{nameof(CrudControllerOption.RoutePath)} cannot be empty.");
        }

        ValidatePagination(options.Pagination, failures);
        ValidateHttpMethods(options.HttpMethods, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePagination(
        AutoControllerPaginationOption pagination,
        ICollection<string> failures)
    {
        if (pagination.DefaultResultCount <= 0)
        {
            failures.Add($"{nameof(AutoControllerPaginationOption.DefaultResultCount)} must be greater than zero.");
        }

        if (pagination.MaximumResultCount < pagination.DefaultResultCount)
        {
            failures.Add(
                $"{nameof(AutoControllerPaginationOption.MaximumResultCount)} must be greater than or equal to " +
                $"{nameof(AutoControllerPaginationOption.DefaultResultCount)}.");
        }

        if (pagination.MaximumCrudResultCount < pagination.DefaultResultCount)
        {
            failures.Add(
                $"{nameof(AutoControllerPaginationOption.MaximumCrudResultCount)} must be greater than or equal to " +
                $"{nameof(AutoControllerPaginationOption.DefaultResultCount)}.");
        }
    }

    private static void ValidateHttpMethods(
        ConventionalHttpMethodOption httpMethods,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(httpMethods.DefaultHttpMethod))
        {
            failures.Add($"{nameof(ConventionalHttpMethodOption.DefaultHttpMethod)} cannot be empty.");
        }
        else if (!IsValidHttpMethod(httpMethods.DefaultHttpMethod))
        {
            failures.Add(
                $"{nameof(ConventionalHttpMethodOption.DefaultHttpMethod)} must be a valid HTTP method token.");
        }

        var prefixOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (httpMethod, prefixes) in httpMethods.Prefixes)
        {
            if (string.IsNullOrWhiteSpace(httpMethod))
            {
                failures.Add("A conventional HTTP method name cannot be empty.");
                continue;
            }

            if (!IsValidHttpMethod(httpMethod))
            {
                failures.Add($"'{httpMethod}' is not a valid conventional HTTP method token.");
                continue;
            }

            foreach (var prefix in prefixes ?? [])
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    failures.Add($"The conventional prefix collection for '{httpMethod}' contains an empty value.");
                    continue;
                }

                if (prefixOwners.TryGetValue(prefix, out var owner) &&
                    !owner.Equals(httpMethod, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"The AutoController action prefix '{prefix}' is assigned to both '{owner}' and '{httpMethod}'.");
                    continue;
                }

                prefixOwners[prefix] = httpMethod;
            }
        }
    }

    private static bool IsValidHttpMethod(string value)
    {
        try
        {
            _ = new HttpMethod(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
