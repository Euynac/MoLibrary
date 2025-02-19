namespace BuildingBlocksPlatform.AutoModel.Model;

public class FieldResult
{
    public static ResultJumpThisField JumpThisField()
    {
        return new ResultJumpThisField();
    }
}


/// <summary>
/// 需要跳过此字段条件（如必定为False的）
/// </summary>
public class ResultJumpThisField
{
    public override string ToString() => nameof(ResultJumpThisField);
}

