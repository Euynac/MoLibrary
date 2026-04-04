using Monica.Repository.GuidGeneration.Abstractions;

namespace Monica.Repository.GuidGeneration.Services;

/// <summary>
/// Implements <see cref="IGuidGenerator"/> by using <see cref="Guid.NewGuid"/>.
/// </summary>
public class RandomGuidGenerator : IGuidGenerator
{
    public static RandomGuidGenerator Instance { get; } = new RandomGuidGenerator();

    public virtual Guid Create()
    {
        return Guid.NewGuid();
    }
}
