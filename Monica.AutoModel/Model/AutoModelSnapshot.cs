namespace Monica.AutoModel.Model;

public class AutoModelSnapshot
{
    public List<AutoField> Fields { get; set; } = [];
    public AutoTable Table { get; set; } = new() { FullTypeName = string.Empty };
}
