using MoLibrary.JobScheduler.EfCore.Entities;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.EfCore.Mappers;

/// <summary>
/// Maps between domain models and EF Core entities.
/// </summary>
public static class JobMetadataMapper
{
    /// <summary>
    /// Converts a JobDefinition domain model to a JobDefinitionEntity.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    /// <param name="existingEntity">Optional existing entity to update. If provided, properties will be updated in place.</param>
    /// <returns>A JobDefinitionEntity with properties mapped from the model.</returns>
    public static JobDefinitionEntity ToEntity(JobDefinition model, JobDefinitionEntity? existingEntity = null)
    {
        var entity = existingEntity ?? new JobDefinitionEntity();

        entity.JobKey = model.JobKey;
        entity.JobArgsKey = model.JobArgsKey;
        entity.FromProject = model.FromProject;
        entity.JobName = model.JobName;
        entity.Description = model.Description;
        entity.JobType = model.JobType;
        entity.MaxConcurrency = model.MaxConcurrency;
        entity.RetryCount = model.RetryCount;
        entity.MaxExecutionTimeoutSeconds = (long)model.MaxExecutionTimeout.TotalSeconds;
        entity.IsDisabled = model.IsDisabled;
        entity.IsDeleted = model.IsDeleted;
        entity.DeletedAt = model.DeletedAt;
        entity.CronExpression = model.CronExpression;
        entity.StartTime = model.StartTime;
        entity.EndTime = model.EndTime;
        entity.MaxRetainedHistoryRecords = model.MaxRetainedHistoryRecords;
        entity.MaxRetentionDays = model.MaxRetentionDays;

        return entity;
    }

    /// <summary>
    /// Converts a JobDefinitionEntity to a JobDefinition domain model.
    /// </summary>
    /// <param name="entity">The entity to convert.</param>
    /// <returns>A JobDefinition domain model with properties mapped from the entity.</returns>
    public static JobDefinition ToModel(JobDefinitionEntity entity)
    {
        return new JobDefinition
        {
            JobKey = entity.JobKey,
            JobArgsKey = entity.JobArgsKey,
            FromProject = entity.FromProject,
            JobName = entity.JobName,
            Description = entity.Description,
            JobType = entity.JobType,
            MaxConcurrency = entity.MaxConcurrency,
            RetryCount = entity.RetryCount,
            MaxExecutionTimeout = TimeSpan.FromSeconds(entity.MaxExecutionTimeoutSeconds),
            IsDisabled = entity.IsDisabled,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt,
            CronExpression = entity.CronExpression,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            MaxRetainedHistoryRecords = entity.MaxRetainedHistoryRecords,
            MaxRetentionDays = entity.MaxRetentionDays
            // Note: JobClrType and JobArgsClrType are not persisted
            // They are set by the module when loading job types at startup
        };
    }

    /// <summary>
    /// Converts a JobInstance domain model to a JobInstanceEntity.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    /// <param name="existingEntity">Optional existing entity to update. If provided, properties will be updated in place.</param>
    /// <returns>A JobInstanceEntity with properties mapped from the model.</returns>
    public static JobInstanceEntity ToEntity(JobInstance model, JobInstanceEntity? existingEntity = null)
    {
        var entity = existingEntity ?? new JobInstanceEntity();

        entity.InstanceId = model.InstanceId;
        entity.JobKey = model.JobKey;
        entity.State = model.State;
        entity.JobArgs = model.JobArgs;
        entity.CreatedAt = model.CreatedAt;
        entity.StartedAt = model.StartedAt;
        entity.CompletedAt = model.CompletedAt;
        entity.ScheduledExecutionTime = model.ScheduledExecutionTime;
        entity.StateHistory = model.StateHistory;
        entity.RetryAttempt = model.RetryAttempt;
        entity.RunningClientId = model.RunningClientId;

        return entity;
    }

    /// <summary>
    /// Converts a JobInstanceEntity to a JobInstance domain model.
    /// </summary>
    /// <param name="entity">The entity to convert.</param>
    /// <returns>A JobInstance domain model with properties mapped from the entity.</returns>
    public static JobInstance ToModel(JobInstanceEntity entity)
    {
        return new JobInstance
        {
            InstanceId = entity.InstanceId,
            JobKey = entity.JobKey,
            State = entity.State,
            JobArgs = entity.JobArgs,
            CreatedAt = entity.CreatedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            ScheduledExecutionTime = entity.ScheduledExecutionTime,
            StateHistory = entity.StateHistory,
            RetryAttempt = entity.RetryAttempt,
            RunningClientId = entity.RunningClientId
        };
    }
}
