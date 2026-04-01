using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.EFCoreExtensions;
using Monica.Repository.EntityInterfaces;
using Monica.Repository.Extensions;
using Monica.Repository.Interfaces;
using Monica.Modules;
using Monica.Repository.Transaction;
using Monica.Repository.Transaction.EntityEvent;
using Monica.Tool.Extensions;
using Monica.Tool.Utils;

namespace Monica.Repository;

public abstract class MoDbContext<TDbContext>(DbContextOptions<TDbContext> options, ICachedServiceProvider serviceProvider) : DbContext(options), IMoDbContext, ITransientDependency
    where TDbContext : DbContext
{
    public ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;

    protected IMoAuditPropertySetter AuditPropertySetter => CachedServiceProvider.GetRequiredService<IMoAuditPropertySetter>();

    protected ILogger<MoDbContext<TDbContext>> Logger => CachedServiceProvider.GetService<ILogger<MoDbContext<TDbContext>>>() ?? NullLogger<MoDbContext<TDbContext>>.Instance;

    protected ModuleRepositoryOption Options => CachedServiceProvider.GetRequiredService<IOptions<ModuleRepositoryOption>>().Value;

    public bool HasInit { get; protected set; }



    protected readonly DbContextOptions DbContextOptions = options;
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //check if is in development
        if ((Options.EnableSensitiveDataLogging is null && UtilsEnvironment.IsDevelopment()) || Options.EnableSensitiveDataLogging is true)
        {
            optionsBuilder.EnableSensitiveDataLogging();//巨坑:这个可以显示具体参数值的设置必须写在OnConfiguring里面才会生效。
        }
    }

    #region 待优化
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(0);
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp");
        configurationBuilder.Properties<TimeOnly>().HavePrecision(0);
        configurationBuilder.Properties<TimeSpan>().HavePrecision(0);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Extend DbContext default field settings
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
                //Set up automatic generation of Snowflake ID
                if (property.Name.Equals("Id") && property.ValueGenerated != ValueGenerated.Never &&
                    property.ClrType == typeof(long))
                {
                    property.SetValueGeneratorFactory((_, _) => new SnowflakeLongValueGenerator());
                    builder.Entity(entityType.ClrType).Property(property.Name).ValueGeneratedNever();
                }

                if (property.ClrType == typeof(string))
                {
                    property.SetDefaultValue("");
                }

                //pgsql will automatically fill in spaces for char type
                if (property.GetColumnType() == "char")
                {
                    property.SetValueConverter(new CharTrimEndValueConverter());
                }

                //Enum conversion database storage as string
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

                //Tidb does not support ascii_general_ci
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
            _configureBasePropertiesMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder, entityType]);

            _configureValueConverterMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder, entityType]);
        }

        //Tidb is used with mysql8.0.0 or above.
        //builder.UseCollation("utf8mb4_bin"); 

        builder.ApplyEntitySelfConfigurations(Options, Logger);

        builder.ApplyEntitySeparateConfigurations(Options, Logger);

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
            var tracker = ChangeTracker;
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
    {
        PublishEventsForTrackedEntity(e.Entry);
    }

    protected virtual void ChangeTracker_StateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        PublishEventsForTrackedEntity(e.Entry);
    }

    protected IAsyncLocalEventPublisher? Publisher => CachedServiceProvider.GetService<IAsyncLocalEventPublisher>();


    #region 触发跟踪增删改时，审计等自动属性设置
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

                //Big Pitfall: In ABP 8.0.2, OnAdd is not considered for new addition judgment, resulting in no triggering of related events.
                if (entry.Properties.Any(x => x is { IsModified: true, Metadata.ValueGenerated: ValueGenerated.Never or ValueGenerated.OnAdd }))
                {
                    //EFCore can get the original value!
                    //entry.OriginalValues

                    //// Skip `PublishEntityDeletedEvent/PublishEntityUpdatedEvent` if only foreign keys have changed.
                    //if (entry.Properties.Where(x => x.IsModified).All(x => x.Metadata.IsForeignKey()))
                    //{
                    //    break;
                    //}

                    if (entry.Entity is IHasSoftDelete && entry.Entity.As<IHasSoftDelete>().IsDeleted)
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
        if (entry.Entity is not IHasConcurrencyStamp entity)
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
        if (entry.State == EntityState.Modified && entry.Properties.Any(x => x is { IsModified: true, Metadata.ValueGenerated: ValueGenerated.Never or ValueGenerated.OnAdd }))
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
        if (entry.Entity is not IHasSoftDelete entity)
        {
            return;
        }

        entry.State = EntityState.Unchanged;
        entity.IsDeleted = true;

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
    #endregion

    #region 实体额外配置
    private static readonly MethodInfo _configureBasePropertiesMethodInfo
        = typeof(MoDbContext<TDbContext>)
            .GetMethod(
                nameof(ConfigureBaseProperties),
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;

    private static readonly MethodInfo _configureValueConverterMethodInfo
        = typeof(MoDbContext<TDbContext>)
            .GetMethod(
                nameof(ConfigureValueConverter),
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;

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
        //TODO Automatic conversion between UTC DateTime type and local time
        //if (mutableEntityType.BaseType == null &&
        //    !typeof(TEntity).IsDefined(typeof(DisableDateTimeNormalizationAttribute), true) &&
        //    !typeof(TEntity).IsDefined(typeof(OwnedAttribute), true) &&
        //    !mutableEntityType.IsOwned())
        //{

        //    //if (CachedServiceProvider == null || Clock == null)
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
            var softDeleteColumnName = modelBuilder.Entity<TEntity>().Metadata.FindProperty(nameof(IHasSoftDelete.IsDeleted))?.GetColumnName() ?? nameof(IHasSoftDelete.IsDeleted);

            if (Options.UseDbFunction)
            {
                expression = e => MoEfCoreDataFilterDbFunctionMethods.SoftDeleteFilter(((IHasSoftDelete)e).IsDeleted, true);
                modelBuilder.ConfigureSoftDeleteDbFunction(MoEfCoreDataFilterDbFunctionMethods.SoftDeleteFilterMethodInfo, true);
            }
            else
            {
                expression = e => !EF.Property<bool>(e, softDeleteColumnName);
            }
        }

        return expression;
    }


    #endregion


}
