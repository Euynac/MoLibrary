namespace BuildingBlocksPlatform.Repository.EntityInterfaces;

public interface IHasConcurrencyStamp
{
    string ConcurrencyStamp { get; set; }
}
