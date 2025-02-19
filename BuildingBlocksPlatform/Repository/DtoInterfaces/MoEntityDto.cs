namespace BuildingBlocksPlatform.Repository.DtoInterfaces;

[Serializable]
public abstract class MoEntityDto : IMoEntityDto
{
    public override string ToString()
    {
        return $"[DTO: {GetType().Name}]";
    }
}

[Serializable]
public abstract class MoEntityDto<TKey> : MoEntityDto, IMoEntityDto<TKey>
{
    /// <summary>
    /// Id of the entity.
    /// </summary>
    public TKey Id { get; set; } = default!;

    public override string ToString()
    {
        return $"[DTO: {GetType().Name}] Id = {Id}";
    }
}
