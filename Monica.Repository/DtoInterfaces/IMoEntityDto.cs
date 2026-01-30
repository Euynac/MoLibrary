namespace Monica.Repository.DtoInterfaces;

public interface IMoEntityDto
{

}

public interface IMoEntityDto<TKey> : IMoEntityDto
{
    TKey Id { get; set; }
}
