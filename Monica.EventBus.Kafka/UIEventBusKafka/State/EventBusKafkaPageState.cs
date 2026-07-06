using Monica.Core.Results;
using Monica.EventBus.Kafka.Facades;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.UIEventBusKafka.State;

/// <summary>
/// Page state for the Kafka EventBus console.
/// </summary>
public sealed class EventBusKafkaPageState(KafkaConsoleFacade facade)
{
    /// <summary>
    /// Current integration snapshot.
    /// </summary>
    public KafkaIntegrationSnapshot Integration { get; private set; } = new();

    /// <summary>
    /// Visible Kafka clusters.
    /// </summary>
    public List<KafkaClusterSummary> Clusters { get; private set; } = [];

    /// <summary>
    /// Selected cluster dashboard.
    /// </summary>
    public KafkaDashboardSnapshot Dashboard { get; private set; } = new();

    /// <summary>
    /// Topics for the selected cluster.
    /// </summary>
    public List<KafkaTopicSummary> Topics { get; private set; } = [];

    /// <summary>
    /// Consumer groups for the selected cluster.
    /// </summary>
    public List<KafkaConsumerGroupSummary> ConsumerGroups { get; private set; } = [];

    /// <summary>
    /// Performance snapshots for the selected cluster.
    /// </summary>
    public List<KafkaPerformanceSnapshot> PerformanceSnapshots { get; private set; } = [];

    /// <summary>
    /// Selected cluster id.
    /// </summary>
    public string? SelectedClusterId { get; private set; }

    /// <summary>
    /// Last operation error message.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Whether the page is loading initial data.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Whether an operation is in progress.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Selected cluster summary.
    /// </summary>
    public KafkaClusterSummary? SelectedCluster =>
        Clusters.FirstOrDefault(cluster => string.Equals(cluster.Config.ClusterId, SelectedClusterId, StringComparison.Ordinal));

    /// <summary>
    /// Whether the selected cluster supports direct Kafka admin operations.
    /// </summary>
    public bool CanAdminSelectedCluster => SelectedCluster?.Config.HasDirectKafkaAccess == true;

    /// <summary>
    /// Initializes all page data.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await RefreshAsync(cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes integration, cluster, dashboard, and selected cluster details.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            if (!TryRead(await facade.GetIntegrationAsync(cancellationToken), out var integration))
            {
                return;
            }

            Integration = integration;

            if (!TryRead(await facade.ListClustersAsync(cancellationToken), out var clusters))
            {
                return;
            }

            Clusters = clusters.ToList();
            SelectedClusterId = ResolveSelectedClusterId();

            if (!TryRead(await facade.GetDashboardAsync(SelectedClusterId, cancellationToken), out var dashboard))
            {
                return;
            }

            Dashboard = dashboard;
            await RefreshSelectedClusterDetailsAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Selects a cluster and loads detail tabs.
    /// </summary>
    public async Task SelectClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        SelectedClusterId = clusterId;
        await RunAsync(async () =>
        {
            if (TryRead(await facade.GetDashboardAsync(clusterId, cancellationToken), out var dashboard))
            {
                Dashboard = dashboard;
            }

            await RefreshSelectedClusterDetailsAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Creates or updates a cluster.
    /// </summary>
    public async Task<bool> UpsertClusterAsync(KafkaClusterConfig cluster, CancellationToken cancellationToken = default)
    {
        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.UpsertClusterAsync(new KafkaClusterUpsertRequest { Cluster = cluster }, cancellationToken), out var summary))
            {
                return false;
            }

            SelectedClusterId = summary.Config.ClusterId;
            await RefreshAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Deletes a cluster.
    /// </summary>
    public async Task<bool> DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.DeleteClusterAsync(clusterId, cancellationToken)))
            {
                return false;
            }

            if (string.Equals(SelectedClusterId, clusterId, StringComparison.Ordinal))
            {
                SelectedClusterId = null;
            }

            await RefreshAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Tests the selected cluster.
    /// </summary>
    public async Task<bool> TestClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.TestClusterAsync(clusterId, cancellationToken), out _))
            {
                return false;
            }

            await RefreshAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Creates a topic in the selected cluster.
    /// </summary>
    public async Task<bool> CreateTopicAsync(KafkaTopicCreateRequest request, CancellationToken cancellationToken = default)
    {
        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.CreateTopicAsync(request, cancellationToken)))
            {
                return false;
            }

            await RefreshTopicsAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Deletes a topic from the selected cluster.
    /// </summary>
    public async Task<bool> DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.DeleteTopicAsync(SelectedClusterId, topicName, cancellationToken)))
            {
                return false;
            }

            await RefreshTopicsAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Increases a topic partition count.
    /// </summary>
    public async Task<bool> IncreasePartitionsAsync(string topicName, int increaseTo, CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            var request = new KafkaTopicPartitionRequest
            {
                ClusterId = SelectedClusterId,
                TopicName = topicName,
                IncreaseTo = increaseTo
            };

            if (!TryRead(await facade.IncreasePartitionsAsync(request, cancellationToken)))
            {
                return false;
            }

            await RefreshTopicsAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Updates topic retention.
    /// </summary>
    public async Task<bool> UpdateRetentionAsync(string topicName, long retentionMs, CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            var request = new KafkaTopicRetentionRequest
            {
                ClusterId = SelectedClusterId,
                TopicName = topicName,
                RetentionMs = retentionMs
            };

            if (!TryRead(await facade.UpdateRetentionAsync(request, cancellationToken)))
            {
                return false;
            }

            await RefreshTopicsAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Reads a bounded sample of recent messages from a selected cluster topic.
    /// </summary>
    public async Task<KafkaTopicMessageBatch?> ReadTopicMessagesAsync(
        string topicName,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return null;
        }

        KafkaTopicMessageBatch? batch = null;
        await RunAsync(async () =>
        {
            var request = new KafkaTopicMessagesRequest
            {
                ClusterId = SelectedClusterId,
                TopicName = topicName,
                MaxMessages = maxMessages
            };

            if (TryRead(await facade.ReadTopicMessagesAsync(request, cancellationToken), out var loadedBatch))
            {
                batch = loadedBatch;
            }
        });

        return batch;
    }

    /// <summary>
    /// Captures and stores a fresh performance snapshot.
    /// </summary>
    public async Task<bool> CapturePerformanceAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.CapturePerformanceAsync(SelectedClusterId, cancellationToken), out _))
            {
                return false;
            }

            await RefreshPerformanceAsync(cancellationToken);
            return true;
        });
    }

    private async Task RefreshSelectedClusterDetailsAsync(CancellationToken cancellationToken)
    {
        if (SelectedClusterId is null || !CanAdminSelectedCluster)
        {
            Topics = [];
            ConsumerGroups = [];
            PerformanceSnapshots = [];
            return;
        }

        await RefreshTopicsAsync(cancellationToken);
        await RefreshConsumerGroupsAsync(cancellationToken);
        await RefreshPerformanceAsync(cancellationToken);
    }

    private async Task RefreshTopicsAsync(CancellationToken cancellationToken)
    {
        if (SelectedClusterId is null)
        {
            Topics = [];
            return;
        }

        if (TryRead(await facade.ListTopicsAsync(SelectedClusterId, cancellationToken), out var topics))
        {
            Topics = topics.ToList();
        }
    }

    private async Task RefreshConsumerGroupsAsync(CancellationToken cancellationToken)
    {
        if (SelectedClusterId is null)
        {
            ConsumerGroups = [];
            return;
        }

        if (TryRead(await facade.ListConsumerGroupsAsync(SelectedClusterId, cancellationToken), out var groups))
        {
            ConsumerGroups = groups.ToList();
        }
    }

    private async Task RefreshPerformanceAsync(CancellationToken cancellationToken)
    {
        if (SelectedClusterId is null)
        {
            PerformanceSnapshots = [];
            return;
        }

        if (TryRead(await facade.GetPerformanceHistoryAsync(SelectedClusterId, cancellationToken: cancellationToken), out var snapshots))
        {
            PerformanceSnapshots = snapshots.ToList();
        }
    }

    private string? ResolveSelectedClusterId()
    {
        if (!string.IsNullOrWhiteSpace(SelectedClusterId) &&
            Clusters.Any(cluster => string.Equals(cluster.Config.ClusterId, SelectedClusterId, StringComparison.Ordinal)))
        {
            return SelectedClusterId;
        }

        if (!string.IsNullOrWhiteSpace(Integration.PrimaryClusterId) &&
            Clusters.Any(cluster => string.Equals(cluster.Config.ClusterId, Integration.PrimaryClusterId, StringComparison.Ordinal)))
        {
            return Integration.PrimaryClusterId;
        }

        return Clusters.FirstOrDefault()?.Config.ClusterId;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RunAsync(Func<Task<bool>> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            return await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryRead(Res result)
    {
        if (!result.IsFailed(out var error))
        {
            return true;
        }

        ErrorMessage = error.Message;
        return false;
    }

    private bool TryRead<T>(Res<T> result, out T data)
    {
        if (!result.IsFailed(out var error))
        {
            data = result.Data!;
            return true;
        }

        data = default!;
        ErrorMessage = error.Message;
        return false;
    }
}
