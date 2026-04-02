namespace Monica.WebApi.AutoControllers.Abstractions;

public interface IHasRequestIds<TKey>
{
    public List<TKey> Ids { get; set; }
}