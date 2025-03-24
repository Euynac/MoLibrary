namespace MoLibrary.Repository.EntityInterfaces;

public interface IHasConcurrencyStamp
{
    string ConcurrencyStamp { get; set; }
}
