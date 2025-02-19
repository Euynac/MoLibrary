namespace BuildingBlocksPlatform.Features.GRPCExtensions;

public interface IHasGrpcHttpInfo
{
    public Dictionary<string, string?>? Headers { get; set; }

    public void AddHeader(string name, string? value = null)
    {
        Headers ??= new Dictionary<string, string?>();
        Headers.Add(name, value);
    }

    public bool IsHeaderExist(string name)
    {
        return Headers?.ContainsKey(name) is true;
    }

    public bool TryGetHeader(string name, out string? value)
    {
        value = null;
        return Headers?.TryGetValue(name, out value) is true;
    }
    public string? GetHeaderValue(string name)
    {
        if (Headers?.TryGetValue(name, out var value) is true) return value;
        return null;
    }
}