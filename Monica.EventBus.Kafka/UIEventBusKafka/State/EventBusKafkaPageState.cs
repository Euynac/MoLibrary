using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.EventBus.Kafka.Facades;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.UIEventBusKafka.State;

/// <summary>
/// Page state for the Kafka EventBus console.
/// </summary>
public sealed class EventBusKafkaPageState(
    KafkaConsoleFacade facade,
    KafkaPerformancePollingState performancePollingState)
{
    private string? _topicsClusterId;
    private string? _consumerGroupsClusterId;
    private string? _performanceClusterId;

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
    public IReadOnlyList<KafkaPerformanceSnapshot> PerformanceSnapshots => performancePollingState.Snapshots;

    /// <summary>
    /// Latest performance snapshot, including captures produced by the live sampler.
    /// </summary>
    public KafkaPerformanceSnapshot? LatestPerformance =>
        performancePollingState.LatestSnapshot ?? Dashboard.LatestPerformance;

    /// <summary>
    /// Current retained-message inventory from the latest live sample or topic enrichment.
    /// </summary>
    public long? TotalAvailableMessageCount =>
        LatestPerformance is { TopicMetrics.Count: > 0 } latest
            ? latest.TotalAvailableMessageCount
            : Dashboard.TotalAvailableMessageCount;

    /// <summary>
    /// Current topic count from the latest live metadata sample or dashboard snapshot.
    /// </summary>
    public int CurrentTopicCount => LatestPerformance?.TopicCount ?? Dashboard.TopicCount;

    /// <summary>
    /// Current consumer-group count from the latest live metadata sample or dashboard snapshot.
    /// </summary>
    public int CurrentConsumerGroupCount => LatestPerformance?.ConsumerGroupCount ?? Dashboard.ConsumerGroupCount;

    /// <summary>
    /// Current broker count from the latest live metadata sample or dashboard snapshot.
    /// </summary>
    public int CurrentBrokerCount => LatestPerformance?.BrokerCount ?? Dashboard.BrokerCount;

    /// <summary>
    /// Whether live performance sampling is enabled.
    /// </summary>
    public bool IsPerformanceAutoRefreshEnabled => performancePollingState.IsAutomaticRefreshEnabled;

    /// <summary>
    /// Current live performance refresh interval in seconds.
    /// </summary>
    public int PerformanceRefreshIntervalSeconds => performancePollingState.RefreshIntervalSeconds;

    /// <summary>
    /// Whether a live performance sample is in progress.
    /// </summary>
    public bool IsPerformanceSampling => performancePollingState.IsSampling;

    /// <summary>
    /// Timestamp of the latest completed live performance sample.
    /// </summary>
    public DateTimeOffset? LastPerformanceCapturedAt => performancePollingState.LastCapturedAt;

    /// <summary>
    /// Latest live sampling diagnostic.
    /// </summary>
    public string? PerformanceSamplingError => performancePollingState.LastError;

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
    public bool CanAdminSelectedCluster => SelectedCluster?.IsReachable == true;

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

            var previousSelectedClusterId = SelectedClusterId;
            Clusters = MergeClusterRuntimeState(clusters);
            SelectedClusterId = ResolveSelectedClusterId();
            var selectedClusterChanged = !string.Equals(previousSelectedClusterId, SelectedClusterId, StringComparison.Ordinal);
            if (selectedClusterChanged)
            {
                ClearSelectedClusterDetails();
            }

            if (!TryRead(await facade.GetDashboardAsync(SelectedClusterId, cancellationToken), out var dashboard))
            {
                return;
            }

            Dashboard = dashboard;
            ApplySelectedClusterToDashboard();
            ApplyLoadedDetailsToDashboard();
            await RefreshSelectedClusterDetailsAsync(cancellationToken);
        });

        await SynchronizePerformancePollingAsync(cancellationToken);
    }

    /// <summary>
    /// Selects a cluster and loads detail tabs.
    /// </summary>
    public async Task SelectClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var requestedCluster = Clusters.FirstOrDefault(cluster =>
            string.Equals(cluster.Config.ClusterId, clusterId, StringComparison.Ordinal));
        if (requestedCluster?.IsReachable != true)
        {
            return;
        }

        SelectedClusterId = clusterId;
        ClearSelectedClusterDetails();
        await RunAsync(async () =>
        {
            if (TryRead(await facade.GetDashboardAsync(clusterId, cancellationToken), out var dashboard))
            {
                Dashboard = dashboard;
                ApplySelectedClusterToDashboard();
            }

            await RefreshSelectedClusterDetailsAsync(cancellationToken);
        });

        await SynchronizePerformancePollingAsync(cancellationToken);
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
        var succeeded = await RunAsync(async () =>
        {
            if (!TryRead(await facade.TestClusterAsync(clusterId, cancellationToken), out var summary))
            {
                return false;
            }

            if (summary.IsReachable)
            {
                ActivateCluster(summary);
                await RefreshSelectedClusterDetailsIfSelectedAsync(
                    summary.Config.ClusterId,
                    cancellationToken,
                    suppressErrors: true);
                return true;
            }

            ApplyClusterSummary(summary);
            ClearSelectedClusterDetailsIfSelected(summary.Config.ClusterId);
            ErrorMessage = summary.ErrorMessage ?? $"Kafka cluster '{summary.Config.DisplayName}' is not reachable.";
            return false;
        });

        await SynchronizePerformancePollingAsync(cancellationToken);
        return succeeded;
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
    /// Clears all retained records from every partition of a selected topic without deleting it.
    /// </summary>
    public async Task<bool> ClearTopicMessagesAsync(
        string topicName,
        CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            if (!TryRead(await facade.ClearTopicMessagesAsync(SelectedClusterId, topicName, cancellationToken)))
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
    /// Gets partition-level retained-message inventory for a selected cluster topic.
    /// </summary>
    public async Task<KafkaTopicBacklogSnapshot?> GetTopicBacklogAsync(
        string topicName,
        CancellationToken cancellationToken = default)
    {
        if (SelectedClusterId is null)
        {
            return null;
        }

        KafkaTopicBacklogSnapshot? snapshot = null;
        await RunAsync(async () =>
        {
            if (TryRead(await facade.GetTopicBacklogAsync(SelectedClusterId, topicName, cancellationToken), out var loadedSnapshot))
            {
                snapshot = loadedSnapshot;
            }
        });

        return snapshot;
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
            if (!await performancePollingState.CaptureNowAsync(cancellationToken))
            {
                ErrorMessage = performancePollingState.LastError;
                return false;
            }

            Dashboard.LatestPerformance = performancePollingState.LatestSnapshot;
            return true;
        });
    }

    /// <summary>
    /// Enables or disables live performance sampling for the selected cluster.
    /// </summary>
    public Task SetPerformanceAutoRefreshAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        return performancePollingState.SetAutomaticRefreshAsync(enabled, cancellationToken);
    }

    /// <summary>
    /// Changes the live performance sampling interval for the current page session.
    /// </summary>
    public Task SetPerformanceRefreshIntervalAsync(
        int seconds,
        CancellationToken cancellationToken = default)
    {
        return performancePollingState.SetRefreshIntervalAsync(seconds, cancellationToken);
    }

    /// <summary>
    /// Stops live sampling when the page is no longer active.
    /// </summary>
    public Task DeactivateAsync()
    {
        return performancePollingState.DeactivateAsync();
    }

    private async Task RefreshSelectedClusterDetailsAsync(CancellationToken cancellationToken, bool suppressErrors = false)
    {
        if (SelectedClusterId is null || !ShouldLoadSelectedClusterDetails)
        {
            ClearSelectedClusterDetails();
            return;
        }

        await RefreshTopicsAsync(cancellationToken, suppressErrors);
        await RefreshConsumerGroupsAsync(cancellationToken, suppressErrors);
        await RefreshPerformanceAsync(cancellationToken, suppressErrors);
    }

    private async Task RefreshSelectedClusterDetailsIfSelectedAsync(
        string clusterId,
        CancellationToken cancellationToken,
        bool suppressErrors = false)
    {
        if (!string.Equals(SelectedClusterId, clusterId, StringComparison.Ordinal))
        {
            return;
        }

        await RefreshSelectedClusterDetailsAsync(cancellationToken, suppressErrors);
    }

    private bool ShouldLoadSelectedClusterDetails => SelectedCluster?.IsReachable == true;

    private void ClearSelectedClusterDetailsIfSelected(string clusterId)
    {
        if (string.Equals(SelectedClusterId, clusterId, StringComparison.Ordinal))
        {
            ClearSelectedClusterDetails();
        }
    }

    private void ClearSelectedClusterDetails()
    {
        Topics = [];
        ConsumerGroups = [];
        performancePollingState.ClearHistory();
        _topicsClusterId = null;
        _consumerGroupsClusterId = null;
        _performanceClusterId = null;
        Dashboard.TotalAvailableMessageCount = null;
        Dashboard.LatestPerformance = null;
    }

    private void ApplyClusterSummary(KafkaClusterSummary summary)
    {
        var index = Clusters.FindIndex(cluster =>
            string.Equals(cluster.Config.ClusterId, summary.Config.ClusterId, StringComparison.Ordinal));
        if (index >= 0)
        {
            Clusters[index] = summary;
        }
        else
        {
            Clusters.Add(summary);
            Clusters = Clusters
                .OrderBy(cluster => cluster.Config.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.Equals(SelectedClusterId, summary.Config.ClusterId, StringComparison.Ordinal))
        {
            return;
        }

        Dashboard.SelectedCluster = summary;
        Dashboard.BrokerCount = summary.BrokerCount;
        Dashboard.TopicCount = summary.TopicCount;
        Dashboard.ConsumerGroupCount = summary.ConsumerGroupCount;
    }

    private void ActivateCluster(KafkaClusterSummary summary)
    {
        SelectedClusterId = summary.Config.ClusterId;
        ClearSelectedClusterDetails();
        DisconnectClustersExcept(summary.Config.ClusterId);
        ApplyClusterSummary(summary);
    }

    private void DisconnectClustersExcept(string connectedClusterId)
    {
        foreach (var cluster in Clusters.Where(cluster =>
                     !string.Equals(cluster.Config.ClusterId, connectedClusterId, StringComparison.Ordinal)))
        {
            cluster.IsReachable = false;
            cluster.BrokerCount = 0;
            cluster.TopicCount = 0;
            cluster.ConsumerGroupCount = 0;
            cluster.ErrorMessage = null;
        }
    }

    private List<KafkaClusterSummary> MergeClusterRuntimeState(IReadOnlyList<KafkaClusterSummary> refreshedClusters)
    {
        var previousClusters = Clusters.ToDictionary(
            cluster => cluster.Config.ClusterId,
            StringComparer.Ordinal);

        return refreshedClusters
            .Select(cluster =>
            {
                if (previousClusters.TryGetValue(cluster.Config.ClusterId, out var previous) &&
                    CanPreserveRuntimeState(previous.Config, cluster.Config))
                {
                    CopyRuntimeState(previous, cluster);
                }

                return cluster;
            })
            .ToList();
    }

    private void ApplySelectedClusterToDashboard()
    {
        var selectedCluster = SelectedCluster;
        if (selectedCluster is null)
        {
            return;
        }

        Dashboard.SelectedCluster = selectedCluster;
        if (selectedCluster.IsReachable)
        {
            Dashboard.BrokerCount = selectedCluster.BrokerCount;
            Dashboard.TopicCount = selectedCluster.TopicCount;
            Dashboard.ConsumerGroupCount = selectedCluster.ConsumerGroupCount;
        }
    }

    private void ApplyLoadedDetailsToDashboard()
    {
        if (string.Equals(_topicsClusterId, SelectedClusterId, StringComparison.Ordinal))
        {
            ApplyTopicBacklogSummary();
        }

        if (string.Equals(_consumerGroupsClusterId, SelectedClusterId, StringComparison.Ordinal))
        {
            Dashboard.ConsumerGroupCount = ConsumerGroups.Count;
        }

        if (string.Equals(_performanceClusterId, SelectedClusterId, StringComparison.Ordinal))
        {
            Dashboard.LatestPerformance = performancePollingState.LatestSnapshot ?? Dashboard.LatestPerformance;
        }
    }

    private static void CopyRuntimeState(KafkaClusterSummary source, KafkaClusterSummary target)
    {
        target.IsReachable = source.IsReachable;
        target.BrokerCount = source.BrokerCount;
        target.TopicCount = source.TopicCount;
        target.ConsumerGroupCount = source.ConsumerGroupCount;
        target.ErrorMessage = source.ErrorMessage;
    }

    private static bool CanPreserveRuntimeState(KafkaClusterConfig previous, KafkaClusterConfig current)
    {
        return string.Equals(previous.BootstrapServers, current.BootstrapServers, StringComparison.Ordinal) &&
               string.Equals(previous.ClientId, current.ClientId, StringComparison.Ordinal) &&
               string.Equals(previous.SecurityProtocol, current.SecurityProtocol, StringComparison.Ordinal) &&
               string.Equals(previous.SaslMechanism, current.SaslMechanism, StringComparison.Ordinal) &&
               string.Equals(previous.SaslUsername, current.SaslUsername, StringComparison.Ordinal) &&
               string.Equals(previous.SaslPassword, current.SaslPassword, StringComparison.Ordinal) &&
               string.Equals(previous.SslCaLocation, current.SslCaLocation, StringComparison.Ordinal) &&
               previous.CredentialsManagedExternally == current.CredentialsManagedExternally;
    }

    private async Task RefreshTopicsAsync(CancellationToken cancellationToken, bool suppressErrors = false)
    {
        if (SelectedClusterId is null)
        {
            Topics = [];
            _topicsClusterId = null;
            return;
        }

        var result = await facade.ListTopicsAsync(SelectedClusterId, cancellationToken);
        if (suppressErrors
                ? TryReadSilently(result, out var topics)
                : TryRead(result, out topics))
        {
            Topics = topics.ToList();
            _topicsClusterId = SelectedClusterId;
            ApplyTopicBacklogSummary();
        }
    }

    private void ApplyTopicBacklogSummary()
    {
        var applicationTopics = Topics
            .Where(topic => !topic.IsInternal)
            .ToList();
        Dashboard.TopicCount = Topics.Count;
        Dashboard.TotalAvailableMessageCount = applicationTopics.Count == 0
            ? 0
            : applicationTopics.All(topic => topic.AvailableMessageCount.HasValue)
                ? applicationTopics.Sum(topic => topic.AvailableMessageCount.GetValueOrDefault())
                : null;
    }

    private async Task RefreshConsumerGroupsAsync(CancellationToken cancellationToken, bool suppressErrors = false)
    {
        if (SelectedClusterId is null)
        {
            ConsumerGroups = [];
            _consumerGroupsClusterId = null;
            return;
        }

        var result = await facade.ListConsumerGroupsAsync(SelectedClusterId, cancellationToken);
        if (suppressErrors
                ? TryReadSilently(result, out var groups)
                : TryRead(result, out groups))
        {
            ConsumerGroups = groups.ToList();
            _consumerGroupsClusterId = SelectedClusterId;
            Dashboard.ConsumerGroupCount = ConsumerGroups.Count;
        }
    }

    private async Task RefreshPerformanceAsync(CancellationToken cancellationToken, bool suppressErrors = false)
    {
        if (SelectedClusterId is null)
        {
            performancePollingState.ClearHistory();
            _performanceClusterId = null;
            return;
        }

        var result = await facade.GetPerformanceHistoryAsync(SelectedClusterId, cancellationToken: cancellationToken);
        if (suppressErrors
                ? TryReadSilently(result, out var snapshots)
                : TryRead(result, out snapshots))
        {
            performancePollingState.SetHistory(snapshots);
            _performanceClusterId = SelectedClusterId;
            Dashboard.LatestPerformance = performancePollingState.LatestSnapshot ?? Dashboard.LatestPerformance;
        }
    }

    private Task SynchronizePerformancePollingAsync(CancellationToken cancellationToken)
    {
        return performancePollingState.ActivateAsync(
            SelectedClusterId,
            ShouldLoadSelectedClusterDetails,
            cancellationToken);
    }

    private string? ResolveSelectedClusterId()
    {
        var selectedCluster = string.IsNullOrWhiteSpace(SelectedClusterId)
            ? null
            : Clusters.FirstOrDefault(cluster => string.Equals(cluster.Config.ClusterId, SelectedClusterId, StringComparison.Ordinal));
        if (selectedCluster?.IsReachable == true)
        {
            return selectedCluster.Config.ClusterId;
        }

        var connectedCluster = Clusters.FirstOrDefault(cluster => cluster.IsReachable);
        if (connectedCluster is not null)
        {
            return connectedCluster.Config.ClusterId;
        }

        if (selectedCluster is not null)
        {
            return selectedCluster.Config.ClusterId;
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
        catch (Exception ex)
        {
            ErrorMessage = ex.GetMessageRecursively();
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
        catch (Exception ex)
        {
            ErrorMessage = ex.GetMessageRecursively();
            return false;
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

    private static bool TryReadSilently<T>(Res<T> result, out T data)
    {
        if (!result.IsFailed(out _))
        {
            data = result.Data!;
            return true;
        }

        data = default!;
        return false;
    }
}
