using System.Text.Json.Serialization;

namespace Monica.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// Implemented by response DTOs that expose a sequence number.
/// </summary>
public interface IHasDtoSequenceNumber
{
    /// <summary>
    /// Sequence number of the current item within the list.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Num { get; set; }
}
