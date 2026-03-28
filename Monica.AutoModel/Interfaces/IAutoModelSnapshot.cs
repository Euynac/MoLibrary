using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

/// <summary>
/// Provides access to registered AutoModel snapshots.
/// </summary>
public interface IAutoModelSnapshotFactory
{
    /// <summary>
    /// Gets all registered AutoModel snapshots.
    /// </summary>
    /// <returns>All registered snapshots.</returns>
    IReadOnlyList<AutoModelSnapshot> GetSnapshots();
}

/// <summary>
/// Represents a strongly typed AutoModel snapshot.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IAutoModelSnapshot<TModel>
{
    /// <summary>
    /// Gets all activation names exposed by the snapshot fields.
    /// </summary>
    /// <returns>All supported activation names.</returns>
    IReadOnlyList<string> GetAllActivateNames();

    /// <summary>
    /// Gets field metadata for the specified activation name.
    /// </summary>
    /// <param name="fieldActivateName">The activation name of the field.</param>
    /// <returns>The matching field metadata, or <c>null</c> if no match exists.</returns>
    AutoField? GetField(string fieldActivateName);

    /// <summary>
    /// Gets field metadata.
    /// </summary>
    /// <param name="fieldActivateNames">Optional field activation names used to filter the result.</param>
    /// <returns>The matching field metadata.</returns>
    IReadOnlyList<AutoField> GetFields(IReadOnlyList<string>? fieldActivateNames = null);
}
