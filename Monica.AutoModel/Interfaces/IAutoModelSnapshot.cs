using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

public interface IAutoModelSnapshotFactory
{
    /// <summary>
    /// Gets all generic AutoModel snapshots.
    /// </summary>
    /// <returns>All registered snapshots.</returns>
    IReadOnlyList<AutoModelSnapshot> GetSnapshots();
}

/// <summary>
/// Generic AutoModel snapshot interface.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IAutoModelSnapshot<TModel>
{
    /// <summary>
    /// Gets all activation names supported by the fields.
    /// </summary>
    /// <returns>All supported activation names.</returns>
    IReadOnlyList<string> GetAllActivateNames();

    /// <summary>
    /// Gets field settings by the specified activation name.
    /// </summary>
    /// <param name="fieldActivateName">The activation name of the field.</param>
    /// <returns>The matching field settings, or <c>null</c> if no match exists.</returns>
    AutoField? GetField(string fieldActivateName);

    /// <summary>
    /// Gets all field settings.
    /// </summary>
    /// <param name="fieldActivateNames">Optional field activation names used to filter the result.</param>
    /// <returns>The matching field settings.</returns>
    IReadOnlyList<AutoField> GetFields(IReadOnlyList<string>? fieldActivateNames = null);
}
