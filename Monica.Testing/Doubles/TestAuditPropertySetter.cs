using Monica.Repository.Entity.Abstractions;

namespace Monica.Testing.Doubles;

/// <summary>
/// No-op audit setter used by repository fixtures when audit behavior is not under test.
/// </summary>
public sealed class TestAuditPropertySetter : IAuditPropertySetter
{
    /// <inheritdoc />
    public void SetCreationProperties(object targetObject)
    {
    }

    /// <inheritdoc />
    public void SetModificationProperties(object targetObject)
    {
    }

    /// <inheritdoc />
    public void SetDeletionProperties(object targetObject)
    {
    }

    /// <inheritdoc />
    public void IncrementEntityVersionProperty(object targetObject)
    {
    }
}
