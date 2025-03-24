using Microsoft.EntityFrameworkCore;
using ShardingCore.Sharding.Abstractions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShardingCore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocksPlatform.DataSync.Interfaces;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Repository.Transaction;
using MoLibrary.Repository;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Interfaces;

namespace BuildingBlocksPlatform.SeedWork;

public abstract class OurHistoryDbContext<TDbContext>(DbContextOptions<TDbContext> options, IMoServiceProvider provider)
    : OurDbContext<TDbContext>(options, provider) where TDbContext : OurDbContext<TDbContext>
{
    protected virtual string? DefaultPostfix => "History";
    protected virtual string? DefaultPrefix => null;

    /// <summary>
    /// 添加实体历史类，注意默认的前缀及后缀设置。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="specificTableName"></param>
    protected virtual void AddEntityHistory<T>(ModelBuilder builder, string? specificTableName = null) where T:class
    {
        var tableName = specificTableName ?? $"{DefaultPrefix}{typeof(T).Name}{DefaultPostfix}";
        builder.Entity<T>().ToTable(tableName);
    }
}



public abstract class OurDbContext<TDbContext>(DbContextOptions<TDbContext> options, IMoServiceProvider provider)
    : MoDbContext<TDbContext>(options, provider), IShardingDbContext
    where TDbContext : MoDbContext<TDbContext>
{
    public IMoDataSyncPropertySetter DataSyncPropertySetter => ServiceProvider.GetRequiredService<IMoDataSyncPropertySetter>();

    #region 分库分表


    private bool _createExecutor = false;
    private IShardingDbContextExecutor? _shardingDbContextExecutor;
    public IShardingDbContextExecutor? GetShardingExecutor()
    {
        if (!_createExecutor)
        {
            _shardingDbContextExecutor = DoCreateShardingDbContextExecutor();
            _createExecutor = true;
        }
        return _shardingDbContextExecutor;
    }
    private IShardingDbContextExecutor? DoCreateShardingDbContextExecutor()
    {
        var shardingDbContextExecutor = this.CreateShardingDbContextExecutor();
        if (shardingDbContextExecutor != null)
        {

            //shardingDbContextExecutor.EntityCreateDbContextBefore += (_, args) =>
            //{
            //    CheckAndSetShardingKeyThatSupportAutoCreate(args.Entity);
            //};
            shardingDbContextExecutor.CreateDbContextAfter += (_, args) =>
            {
                //不是分表操作应该可以不生成新的连接？ 但测试发现必须创建，否则Track的事件无法触发。似乎这才是真正执行的DbContext？
                //if(!args.RouteTail.IsShardingTableQuery()) return;

                var dbContext = args.DbContext;
                if (dbContext is not MoDbContext<TDbContext> moDbContext) return;

                var manager = ServiceProvider.GetRequiredService<IMoUnitOfWorkManager>();
                if (dbContext is IMoDbContext coreDbContext && manager.Current != null && !moDbContext.HasInit) //对于同一个分表RouteTail操作，会进入两次该创建DbContext事件，ShardingCore的实现问题。
                {
                    coreDbContext.Initialize(manager.Current);
                }
            };
        }
        return shardingDbContextExecutor;
    }

    //https://www.cnblogs.com/xuejiaming/p/16450663.html#%E9%9B%86%E6%88%90AbpVNext
    //https://www.cnblogs.com/xuejiaming/p/15449819.html
    //因为ShardingCore需要add,update,remove的时候shardingkey不可以为空。abp提供的字段需要在分库分表的时候先自动填充
    //private void CheckAndSetShardingKeyThatSupportAutoCreate<TEntity>(TEntity entity) where TEntity : class
    //{
    //    if (entity is IShardingKeyIsGuId)
    //    {

    //        if (entity is IEntity<Guid> guidEntity)
    //        {
    //            if (guidEntity.Id != default)
    //            {
    //                return;
    //            }
    //            var idProperty = entity.GetObjectProperty(nameof(IEntity<Guid>.Id));

    //            var dbGeneratedAttr = ReflectionHelper
    //                .GetSingleAttributeOrDefault<DatabaseGeneratedAttribute>(
    //                    idProperty
    //                );

    //            if (dbGeneratedAttr != null && dbGeneratedAttr.DatabaseGeneratedOption != DatabaseGeneratedOption.None)
    //            {
    //                return;
    //            }

    //            EntityHelper.TrySetId(
    //                guidEntity,
    //                () => GuidGenerator.Create(),
    //                true
    //            );
    //        }
    //    }
    //    else if (entity is IShardingKeyIsCreationTime)
    //    {
    //        AuditPropertySetter?.SetCreationProperties(entity);
    //    }
    //}

    public override void Dispose()
    {
        _shardingDbContextExecutor?.Dispose();
        base.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_shardingDbContextExecutor != null)
        {
            await _shardingDbContextExecutor.DisposeAsync();
        }
        await base.DisposeAsync();
    }
    #endregion

    protected override void HandlePropertiesBeforeSave()
    {
        if (GetShardingExecutor() == null) //巨坑：修复ShardingCore调用两次问题。ShardingStateManager.SaveChangesAsync会创建IDbContextTransaction然后
        {
            base.HandlePropertiesBeforeSave();
        }
    }

    protected override void PublishEventsForTrackedEntity(EntityEntry entry)
    {
        if (entry.State != EntityState.Unchanged && entry.Entity is ISystemEntityDataSync)
        {
            DataSyncPropertySetter.SetDataSyncProperties(entry.Entity);
        }

        base.PublishEventsForTrackedEntity(entry);

    }
}


 //#region 修复ABP BUG
 //   protected override void FillExtraPropertiesForTrackedEntities(EntityTrackedEventArgs e)
 //   {
 //       var entityType = e.Entry.Metadata.ClrType;

 //       if (!(e.Entry.Entity is IHasExtraProperties entity))
 //       {
 //           return;
 //       }

 //       if (!e.FromQuery)
 //       {
 //           return;
 //       }

 //       var objectExtension = ObjectExtensionManager.Instance.GetOrNull(entityType);
 //       if (objectExtension == null)
 //       {
 //           return;
 //       }

 //       foreach (var property in objectExtension.GetProperties())
 //       {
 //           if (!property.IsMappedToFieldForEfCore())
 //           {
 //               continue;
 //           }

 //           /* Checking "currentValue != null" has a good advantage:
 //            * Assume that you we already using a named extra property,
 //            * then decided to create a field (entity extension) for it.
 //            * In this way, it prevents to delete old value in the JSON and
 //            * updates the field on the next save!
 //            */

 //           var currentValue = e.Entry.CurrentValues[property.Name];
 //           if (currentValue != null)
 //           {
 //               entity.ExtraProperties[property.Name] = currentValue;
 //           }
 //       }
 //   }
 //   protected override void ChangeTracker_Tracked(object? sender, EntityTrackedEventArgs e)
 //   {
 //       FillExtraPropertiesForTrackedEntities(e);
 //       PublishEventsForTrackedEntity(e.Entry);
 //   }

 //   protected override void ChangeTracker_StateChanged(object? sender, EntityStateChangedEventArgs e)
 //   {
 //       PublishEventsForTrackedEntity(e.Entry);
 //   }

 //   protected IAsyncLocalEventPublisher? Publisher => LazyServiceProvider.LazyGetService<IAsyncLocalEventPublisher>();

 //   protected virtual void PublishEventsForTrackedEntity(EntityEntry entry)
 //   {
 //       switch (entry.State)
 //       {
 //           case EntityState.Added:
 //               ApplyAbpConceptsForAddedEntity(entry);
 //               //EntityChangeEventHelper.PublishEntityCreatedEvent(entry.Entity);
 //               Publisher?.AddEntityCreatedEvent(entry.Entity);
 //               break;

 //           case EntityState.Modified:
 //               ApplyAbpConceptsForModifiedEntity(entry);
 //               //巨坑：ABP 8.0.2中对于新增判断没有考虑OnAdd的情况，导致不会触发相关事件
 //               if (entry.Properties.Any(x => x is { IsModified: true, Metadata.ValueGenerated: ValueGenerated.Never or ValueGenerated.OnAdd }))
 //               {
 //                   //EFCore 可获取原始值！
 //                   //entry.OriginalValues

 //                   //// Skip `PublishEntityDeletedEvent/PublishEntityUpdatedEvent` if only foreign keys have changed.
 //                   //if (entry.Properties.Where(x => x.IsModified).All(x => x.Metadata.IsForeignKey()))
 //                   //{
 //                   //    break;
 //                   //}

 //                   if (entry.Entity is ISoftDelete && entry.Entity.As<ISoftDelete>().IsDeleted)
 //                   {
 //                       //EntityChangeEventHelper.PublishEntityDeletedEvent(entry.Entity);
 //                       Publisher?.AddEntityDeletedEvent(entry.Entity);

 //                   }
 //                   else
 //                   {
 //                       //EntityChangeEventHelper.PublishEntityUpdatedEvent(entry.Entity);
 //                       Publisher?.AddEntityUpdatedEvent(entry.Entity);
 //                   }
 //               }
 //               break;

 //           case EntityState.Deleted:
 //               ApplyAbpConceptsForDeletedEntity(entry);
 //               //EntityChangeEventHelper.PublishEntityDeletedEvent(entry.Entity);
 //               Publisher?.AddEntityDeletedEvent(entry.Entity);
 //               break;
 //       }
 //   }

 //   protected override void UpdateConcurrencyStamp(EntityEntry entry)
 //   {
 //       var entity = entry.Entity as IHasConcurrencyStamp;
 //       if (entity == null)
 //       {
 //           return;
 //       }

 //       Entry(entity).Property(x => x.ConcurrencyStamp).OriginalValue = entity.ConcurrencyStamp;
 //       entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
 //   }

 //   protected override void SetConcurrencyStampIfNull(EntityEntry entry)
 //   {
 //       var entity = entry.Entity as IHasConcurrencyStamp;
 //       if (entity == null)
 //       {
 //           return;
 //       }

 //       if (entity.ConcurrencyStamp != null)
 //       {
 //           return;
 //       }

 //       entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
 //   }


 //   #endregion