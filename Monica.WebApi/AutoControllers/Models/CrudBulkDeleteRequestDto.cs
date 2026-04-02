using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Request DTO for bulk deletion.
/// </summary>
public class CrudBulkDeleteRequestDto<TKey> : IHasRequestIds<TKey>
{
    public List<TKey> Ids { get; set; } = [];
}
