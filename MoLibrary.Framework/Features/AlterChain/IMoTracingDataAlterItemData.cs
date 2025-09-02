namespace MoLibrary.Framework.Features.AlterChain;

public interface IMoTracingDataAlterItemData<in TEntity> where TEntity : IMoTracingDataEntity
{
    public void Apply(TEntity entity);
}