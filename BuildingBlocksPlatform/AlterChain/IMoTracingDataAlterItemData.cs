namespace BuildingBlocksPlatform.AlterChain;

public interface IMoTracingDataAlterItemData<in TEntity> where TEntity : IMoTracingDataEntity
{
    public void Apply(TEntity entity);
}