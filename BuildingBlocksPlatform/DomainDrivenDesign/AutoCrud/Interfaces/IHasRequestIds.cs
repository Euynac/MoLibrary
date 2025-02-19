namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;

public interface IHasRequestIds<TKey>
{
    public List<TKey> Ids { get; set; }
}