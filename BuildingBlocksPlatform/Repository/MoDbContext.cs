using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BuildingBlocksPlatform.DependencyInjection;
using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.Features;
using BuildingBlocksPlatform.Features.MoSnowflake;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Extensions;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using BuildingBlocksPlatform.Transaction;
using BuildingBlocksPlatform.Transaction.EntityEvent;
using BuildingBlocksPlatform.Utils;
using Koubot.Tool.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace BuildingBlocksPlatform.Repository;


public abstract class MoDbContext<TDbContext>(DbContextOptions<TDbContext> options, IMoServiceProvider serviceProvider) : DbContext(options), IMoDbContext, ITransientDependency, IMoServiceProviderAccessor
    where TDbContext : DbContext
{

    public IServiceProvider ServiceProvider { get; set; } = serviceProvider.ServiceProvider;

    public IMoAuditPropertySetter AuditPropertySetter => ServiceProvider.GetRequiredService<IMoAuditPropertySetter>();

    public ILogger<MoDbContext<TDbContext>> Logger => ServiceProvider.GetRequiredService<ILogger<MoDbContext<TDbContext>>>();

    public MoRepositoryOptions MoOptions =>
        ServiceProvider.GetRequiredService<IOptions<MoRepositoryOptions>>().Value;

    public bool HasInit { get; protected set; }


    private static readonly MethodInfo ConfigureBasePropertiesMethodInfo
        = typeof(MoDbContext<TDbContext>)
            .GetMethod(
                nameof(ConfigureBaseProperties),
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;

    private static readonly MethodInfo ConfigureValueConverterMethodInfo
        = typeof(MoDbContext<TDbContext>)
            .GetMethod(
                nameof(ConfigureValueConverter),
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;

    protected readonly DbContextOptions DbContextOptions = options;
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //check if is in development
        if (UtilsEnvironment.IsDevelopment())
        {
            optionsBuilder.EnableSensitiveDataLogging();//巨坑:这个可以显示具体参数值的设置必须写在OnConfiguring里面才会生效。
        }
    }

    #region 待优化
    public class OurLongValueGenerator : ValueGenerator<long>
    {
        public override long Next(EntityEntry entry)
        {
            var snowflake = entry.Context.GetService<ISnowflakeGenerator>();
            return snowflake.GenerateSnowflakeId();
        }

        public override bool GeneratesTemporaryValues => false;
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(0);
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp");
        configurationBuilder.Properties<TimeOnly>().HavePrecision(0);
        configurationBuilder.Properties<TimeSpan>().HavePrecision(0);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// 扩展DbContext默认字段设置
    /// </summary>
    /// <param name="builder"></param>
    protected virtual void OnModelCreatingExtend(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        //set all string property default value to ""
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {


                //设置自动生成雪花ID
                if (property.Name.Equals("Id") && property.ValueGenerated != ValueGenerated.Never &&
                    property.ClrType == typeof(long))
                {
                    property.SetValueGeneratorFactory((_, _) => new OurLongValueGenerator());
                    builder.Entity(entityType.ClrType).Property(property.Name).ValueGeneratedNever();
                }

                if (property.ClrType == typeof(string))
                {
                    property.SetDefaultValue("");
                }

                //pgsql对于char类型会自动补空格
                if (property.GetColumnType() == "char")
                {
                    property.SetValueConverter(new CharTrimEndValueConverter());
                }

                //Enum转换 数据库存储为字符串
                if (property.ClrType.BaseType == typeof(Enum))
                {
                    var columnType = property.GetColumnType();
                    if (columnType == "varchar")
                    {
                        var type = typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType);
                        var converter = Activator.CreateInstance(type, new ConverterMappingHints()) as ValueConverter;
                        property.SetValueConverter(converter);
                    }
                }

                //Tidb不支持ascii_general_ci
                // if (property.ClrType == typeof(Guid?))
                // {
                //     property.SetCollation("utf8mb4_bin");
                // }

                //postgresql 
                //if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                //{
                //    property.SetColumnType("timestamp");
                //}
            }
        }
    }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            ConfigureBasePropertiesMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder, entityType]);

            ConfigureValueConverterMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder, entityType]);
        }

        //Tidb与mysql8.0.0以上版本使用。
        //builder.UseCollation("utf8mb4_bin"); 

        GlobalLog.LogDebug("On model creating...");
        GlobalLog.LogDebug($"Auto load all configurations of DbContext: {typeof(TDbContext).FullName}");

        var entityTypes = builder.Model.GetEntityTypes().Select(p => p.Name).ToHashSet();

        //var entityTypeNameList = builder.Model.GetEntityTypes().Select(p => p.ClrType.FullName).ToHashSet();
        foreach (var assembly in builder.Model.GetEntityTypes().Select(p => Assembly.GetAssembly(p.ClrType))
                     .DistinctBy(a => a!.FullName).ToList())
        {
            GlobalLog.LogDebug($"loading from assembly:{assembly?.FullName}");
            //规范：只有加入DbContext的Entity才会自动读取配置。且配置名必须以TypeConfiguration结尾
            builder.ApplyConfigurationsFromAssembly(assembly!, t =>
            {
                if (t.FullName is not null && t.FullName.EndsWith("TypeConfiguration"))
                {
                    foreach (var type in t.GetInterfaces())
                    {
                        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                                               && type.GenericTypeArguments.FirstOrDefault() is { FullName: not null } entity
                                               && entityTypes.Contains(entity.FullName))
                        {
                            return true;
                        }
                    }
                }

                return false;
            });
        }

        OnModelCreatingExtend(builder);
    }


    #endregion


    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            //foreach (var entityEntry in AbpEfCoreNavigationHelper.GetChangedEntityEntries())
            //{
            //    if (EntityChangeOptions.Value.PublishEntityUpdatedEventWhenNavigationChanges)
            //    {
            //        if (entityEntry.Entity is ISoftDelete && entityEntry.Entity.As<ISoftDelete>().IsDeleted)
            //        {
            //            EntityChangeEventHelper.PublishEntityDeletedEvent(entityEntry.Entity);
            //        }
            //        else
            //        {
            //            EntityChangeEventHelper.PublishEntityUpdatedEvent(entityEntry.Entity);
            //        }
            //    }
            //    else if (entityEntry.Properties.Any(x => x.IsModified && (x.Metadata.ValueGenerated == ValueGenerated.Never || x.Metadata.ValueGenerated == ValueGenerated.OnAdd)))
            //    {
            //        if (entityEntry.Properties.Where(x => x.IsModified).All(x => x.Metadata.IsForeignKey()))
            //        {
            //            // Skip `PublishEntityDeletedEvent/PublishEntityUpdatedEvent` if only foreign keys have changed.
            //            break;
            //        }

            //        if (entityEntry.Entity is ISoftDelete && entityEntry.Entity.As<ISoftDelete>().IsDeleted)
            //        {
            //            EntityChangeEventHelper.PublishEntityDeletedEvent(entityEntry.Entity);
            //        }
            //        else
            //        {
            //            EntityChangeEventHelper.PublishEntityUpdatedEvent(entityEntry.Entity);
            //        }
            //    }
            //}

            //var auditLog = AuditingManager?.Current?.Log;
            //List<EntityChangeInfo>? entityChangeList = null;
            //if (auditLog != null)
            //{
            //    EntityHistoryHelper.InitializeNavigationHelper(AbpEfCoreNavigationHelper);
            //    entityChangeList = EntityHistoryHelper.CreateChangeList(ChangeTracker.Entries().ToList());
            //}

            HandlePropertiesBeforeSave();

            //var eventReport = CreateEventReport();

            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            //var method = typeof(DbContext).GetMethod(nameof(DbContext.SaveChangesAsync), [typeof(bool), typeof(CancellationToken)])!.MethodHandle.GetFunctionPointer();
            //var baseMethod = (Func<int>) Activator.CreateInstance(typeof(Func<int>), this, method)!;
            //var result = baseMethod();




            //PublishEntityEvents(eventReport);

            //if (entityChangeList != null)
            //{
            //    EntityHistoryHelper.UpdateChangeList(entityChangeList);
            //    auditLog!.EntityChanges.AddRange(entityChangeList);
            //    Logger.LogDebug($"Added {entityChangeList.Count} entity changes to the current audit log");
            //}

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (ex.Entries.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ex.Entries.Count > 1
                    ? "There are some entries which are not saved due to concurrency exception:"
                    : "There is an entry which is not saved due to concurrency exception:");
                foreach (var entry in ex.Entries)
                {
                    sb.AppendLine(entry.ToString());
                }

                Logger.LogWarning(sb.ToString());
            }

            throw new Exception(ex.Message, ex);
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = true;
            //AbpEfCoreNavigationHelper.Clear();
        }
    }
    
    /// <summary>
    /// This method will call the DbContext <see cref="SaveChangesAsync(bool, CancellationToken)"/> method directly of EF Core, which doesn't apply concepts of abp.
    /// </summary>
    public virtual Task<int> SaveChangesOnDbContextAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public virtual void Initialize(IMoUnitOfWork unitOfWork)
    {
        if (HasInit) throw new InvalidOperationException("重复触发相同DbContext初始化设置，代码结构异常，请上报");
        HasInit = true;

        if (unitOfWork.Options.Timeout.HasValue &&
            Database.IsRelational() &&
            !Database.GetCommandTimeout().HasValue)
        {
            Database.SetCommandTimeout(TimeSpan.FromMilliseconds(unitOfWork.Options.Timeout.Value));
        }

        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;

        ChangeTracker.Tracked += ChangeTracker_Tracked;
        ChangeTracker.StateChanged += ChangeTracker_StateChanged;
    }


    protected virtual void ChangeTracker_Tracked(object? sender, EntityTrackedEventArgs e)
    {;
        PublishEventsForTrackedEntity(e.Entry);
    }

    protected virtual void ChangeTracker_StateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        PublishEventsForTrackedEntity(e.Entry);
    }

    protected IAsyncLocalEventPublisher? Publisher => ServiceProvider.GetRequiredService<IAsyncLocalEventPublisher>();

    protected virtual void PublishEventsForTrackedEntity(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                ApplyConceptsForAddedEntity(entry);
                Publisher?.AddEntityCreatedEvent(entry.Entity);
                break;

            case EntityState.Modified:
                ApplyConceptsForModifiedEntity(entry);

                //巨坑：ABP 8.0.2中对于新增判断没有考虑OnAdd的情况，导致不会触发相关事件
                if (entry.Properties.Any(x => x is { IsModified: true, Metadata.ValueGenerated: ValueGenerated.Never or ValueGenerated.OnAdd}))
                {
                    //EFCore 可获取原始值！
                    //entry.OriginalValues
                    
                    //// Skip `PublishEntityDeletedEvent/PublishEntityUpdatedEvent` if only foreign keys have changed.
                    //if (entry.Properties.Where(x => x.IsModified).All(x => x.Metadata.IsForeignKey()))
                    //{
                    //    break;
                    //}

                    if (entry.Entity is IHasSoftDelete && entry.Entity.AsCast<IHasSoftDelete>()!.IsDeleted)
                    {
                        Publisher?.AddEntityDeletedEvent(entry.Entity);

                    }
                    else
                    {
                        Publisher?.AddEntityUpdatedEvent(entry.Entity);
                    }
                }
                break;

            case EntityState.Deleted:
                ApplyConceptsForDeletedEntity(entry);
                Publisher?.AddEntityDeletedEvent(entry.Entity);
                break;
        }
    }

    protected virtual void UpdateConcurrencyStamp(EntityEntry entry)
    {
        var entity = entry.Entity as IHasConcurrencyStamp;
        if (entity == null)
        {
            return;
        }

        Entry(entity).Property(x => x.ConcurrencyStamp).OriginalValue = entity.ConcurrencyStamp;
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    protected virtual void SetConcurrencyStampIfNull(EntityEntry entry)
    {
        if (entry.Entity is not IHasConcurrencyStamp entity)
        {
            return;
        }

        if (entity.ConcurrencyStamp.IsNotNullOrEmpty())
        {
            return;
        }

        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    protected virtual void HandlePropertiesBeforeSave()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                UpdateConcurrencyStamp(entry);
            }
        }
    }

    protected virtual void ApplyConceptsForAddedEntity(EntityEntry entry)
    {
        SetConcurrencyStampIfNull(entry);
        SetCreationAuditProperties(entry);
    }

    protected virtual void ApplyConceptsForModifiedEntity(EntityEntry entry)
    {
        if (entry.State == EntityState.Modified && entry.Properties.Any(x => x is {IsModified: true, Metadata.ValueGenerated: ValueGenerated.Never or ValueGenerated.OnAdd}))
        {
            IncrementEntityVersionProperty(entry);
            SetModificationAuditProperties(entry);

            if (entry.Entity is IHasSoftDelete && entry.Entity.As<IHasSoftDelete>().IsDeleted)
            {
                SetDeletionAuditProperties(entry);
            }
        }
    }

    protected virtual void ApplyConceptsForDeletedEntity(EntityEntry entry)
    {
        if (entry.Entity is not IHasSoftDelete)
        {
            return;
        }

        //TODO 这里需要测试是否可以仅通过把Entry改回Unchanged触发Modified即可，而不用Deleted
        //entry.Reload();
        entry.State = EntityState.Unchanged;
        ObjectHelper.TrySetProperty(entry.Entity.As<IHasSoftDelete>(), x => x.IsDeleted, () => true);
        SetDeletionAuditProperties(entry);
    }
    
    protected virtual void SetCreationAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetCreationProperties(entry.Entity);
    }

    protected virtual void SetModificationAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
    }

    protected virtual void SetDeletionAuditProperties(EntityEntry entry)
    {
        AuditPropertySetter?.SetDeletionProperties(entry.Entity);
    }

    protected virtual void IncrementEntityVersionProperty(EntityEntry entry)
    {
        AuditPropertySetter?.IncrementEntityVersionProperty(entry.Entity);
    }

    protected virtual void ConfigureBaseProperties<TEntity>(ModelBuilder modelBuilder, IMutableEntityType mutableEntityType)
        where TEntity : class
    {
        if (mutableEntityType.IsOwned())
        {
            return;
        }

        if (!typeof(IMoEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return;
        }

        modelBuilder.Entity<TEntity>().ConfigureByConvention();

        ConfigureGlobalFilters<TEntity>(modelBuilder, mutableEntityType);
    }

    /// <summary>
    /// Configures global filters for given entity.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="modelBuilder"></param>
    /// <param name="mutableEntityType"></param>
    protected virtual void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder, IMutableEntityType mutableEntityType)
        where TEntity : class
    {
        if (mutableEntityType.BaseType == null && ShouldFilterEntity<TEntity>(mutableEntityType))
        {
            var filterExpression = CreateFilterExpression<TEntity>(modelBuilder);
            if (filterExpression != null)
            {
                modelBuilder.Entity<TEntity>().HasMoQueryFilter(filterExpression);
            }
        }
    }

    protected virtual void ConfigureValueConverter<TEntity>(ModelBuilder modelBuilder, IMutableEntityType mutableEntityType)
        where TEntity : class
    {
        //TODO 自动UTC DateTime类型与本地时间的转换
        //if (mutableEntityType.BaseType == null &&
        //    !typeof(TEntity).IsDefined(typeof(DisableDateTimeNormalizationAttribute), true) &&
        //    !typeof(TEntity).IsDefined(typeof(OwnedAttribute), true) &&
        //    !mutableEntityType.IsOwned())
        //{
           
        //    //if (LazyServiceProvider == null || Clock == null)
        //    //{
        //    //    return;
        //    //}

        //    //foreach (var property in mutableEntityType.GetProperties().
        //    //             Where(property => property.PropertyInfo != null &&
        //    //                               (property.PropertyInfo.PropertyType == typeof(DateTime) || property.PropertyInfo.PropertyType == typeof(DateTime?)) &&
        //    //                               property.PropertyInfo.CanWrite &&
        //    //                               ReflectionHelper.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<DisableDateTimeNormalizationAttribute>(property.PropertyInfo) == null))
        //    //{
        //    //    modelBuilder
        //    //        .Entity<TEntity>()
        //    //        .Property(property.Name)
        //    //        .HasConversion(property.ClrType == typeof(DateTime)
        //    //            ? new AbpDateTimeValueConverter(Clock)
        //    //            : new AbpNullableDateTimeValueConverter(Clock));
        //    //}
        //}
    }

    /// <summary>
    /// Checks if given entity should be filtered.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entityType"></param>
    /// <returns></returns>
    protected virtual bool ShouldFilterEntity<TEntity>(IMutableEntityType entityType) where TEntity : class
    {
        // TODO 
        //var scopedData = ServiceProvider.GetRequiredService<IScopedData>();
        //if (scopedData.DataDict.ContainsKey("disableFilter")) return false;

        if (typeof(IHasSoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a filter expression for given entity.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="modelBuilder"></param>
    /// <returns></returns>
    protected virtual Expression<Func<TEntity, bool>>? CreateFilterExpression<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        Expression<Func<TEntity, bool>>? expression = null;

        if (typeof(IHasSoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            var softDeleteColumnName = modelBuilder.Entity<TEntity>().Metadata.FindProperty(nameof(IHasSoftDelete.IsDeleted))?.GetColumnName() ?? "IsDeleted";
            
            if (MoOptions.UseDbFunction)
            {
                expression = e => MoEfCoreDataFilterDbFunctionMethods.SoftDeleteFilter(((IHasSoftDelete) e).IsDeleted, true);
                modelBuilder.ConfigureSoftDeleteDbFunction(MoEfCoreDataFilterDbFunctionMethods.SoftDeleteFilterMethodInfo);
            }
            else
            {
                expression = e => !EF.Property<bool>(e, softDeleteColumnName);
            }
        }

        return expression;
    }

}