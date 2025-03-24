using Check = MoLibrary.Tool.Utils.Check;

namespace BuildingBlocksPlatform.Authority.Authorization;

public class MultiplePermissionGrantResult
{
    public bool AllGranted
    {
        get
        {
            return Result.Values.All(x => x == EPermissionGrantResult.Granted);
        }
    }

    public bool AllProhibited
    {
        get
        {
            return Result.Values.All(x => x == EPermissionGrantResult.Prohibited);
        }
    }

    public Dictionary<string, EPermissionGrantResult> Result { get; }

    public MultiplePermissionGrantResult()
    {
        Result = [];
    }

    public MultiplePermissionGrantResult(string[] names, EPermissionGrantResult grantResult = EPermissionGrantResult.Undefined)
    {
        Check.NotNull(names, nameof(names));

        Result = [];

        foreach (var name in names)
        {
            Result.Add(name, grantResult);
        }
    }
}
