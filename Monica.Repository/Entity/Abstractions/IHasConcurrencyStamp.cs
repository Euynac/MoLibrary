namespace Monica.Repository.Entity.Abstractions;

public interface IHasConcurrencyStamp
{
    string ConcurrencyStamp { get; set; }
}
