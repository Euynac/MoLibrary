using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.Tool.Extensions;

namespace Monica.StateStore.CancellationManager;

/// <summary>
/// Implementation of memory version cancellation token manager
/// Uses memory storage and event mechanism to provide instant response without polling
/// </summary>
/// <remarks>
/// Note: This implementation is only applicable to single-process/single-instance scenarios and does not support cross-process distributed cancellation.
/// </remarks>
/// <param name="logger">Logger</param>
public class InMemoryCancellationManager(ILogger<InMemoryCancellationManager> logger) : IMoCancellationManager
{
    /// <summary>
    /// In-memory storage of cancellation token state
    /// </summary>
    private readonly ConcurrentDictionary<string, InMemoryTokenState> _tokenStates = new();
    
    /// <summary>
    /// Local cancellation token source cache
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokenSources = new();

    /// <summary>
    /// Create or obtain a cancellation token for the specified key
    /// </summary>
    /// <param name="key">Unique identification key for cancellation token</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Returns the cancellation token associated with the specified key</returns>
    public Task<CancellationToken> GetOrCreateTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // Get or create token status
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now
        });

        // Get or create a cancellation token source
        var tokenSource = _tokenSources.GetOrAdd(key, k =>
        {
            var source = new CancellationTokenSource();
            
            // If the status has been canceled, immediately cancel the newly created token source
            if (tokenState.IsCancelled)
            {
                source.Cancel();
                logger.LogDebug("Immediately cancelled token for key: {Key} due to existing cancelled state", key);
            }
            
            logger.LogDebug("Created new cancellation token for key: {Key}", key);
            return source;
        });

        return Task.FromResult(tokenSource.Token);
    }

    /// <summary>
    /// Cancels the cancellation token for the specified key
    /// </summary>
    /// <param name="key">Unique identification key for cancellation token</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public Task CancelTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // update status
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.Now
        });

        tokenState.IsCancelled = true;
        tokenState.LastUpdatedAt = DateTime.Now;
        tokenState.Version++;

        // Cancel the local token source (if it exists)
        if (_tokenSources.TryGetValue(key, out var tokenSource))
        {
            if (!tokenSource.Token.IsCancellationRequested)
            {
                tokenSource.Cancel();
                logger.LogInformation("Cancelled token for key: {Key}", key);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether the cancellation token for the specified key has been canceled
    /// </summary>
    /// <param name="key">Unique identification key for cancellation token</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Returns true if canceled, false otherwise</returns>
    public Task<bool> IsCancelledAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        var isCancelled = _tokenStates.TryGetValue(key, out var state) && state.IsCancelled;
        return Task.FromResult(isCancelled);
    }

    /// <summary>
    /// Resets the cancellation token status of the specified key
    /// </summary>
    /// <param name="key">Unique identification key for cancellation token</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public Task ResetTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // reset state
        var tokenState = _tokenStates.GetOrAdd(key, k => new InMemoryTokenState
        {
            Key = k,
            CreatedAt = DateTime.Now
        });

        tokenState.IsCancelled = false;
        tokenState.LastUpdatedAt = DateTime.Now;
        tokenState.Version++;

        // Remove and recreate the cancellation token source
        if (_tokenSources.TryRemove(key, out var oldTokenSource))
        {
            oldTokenSource.SafeCancelAndDispose();
        }

        var newTokenSource = new CancellationTokenSource();
        _tokenSources.TryAdd(key, newTokenSource);

        logger.LogInformation("Reset token for key: {Key}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the cancellation token for the specified key
    /// </summary>
    /// <param name="key">Unique identification key for cancellation token</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Token key cannot be null or empty", nameof(key));

        // Remove status
        _tokenStates.TryRemove(key, out _);

        // Remove and clean the cancellation token source
        if (_tokenSources.TryRemove(key, out var tokenSource))
        {
            tokenSource.SafeCancelAndDispose();
        }

        logger.LogDebug("Deleted token for key: {Key}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get a list of keys for all active cancellation tokens
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Returns a list of keys for all active cancellation tokens</returns>
    public Task<IReadOnlyList<string>> GetActiveTokenKeysAsync(CancellationToken cancellationToken = default)
    {
        var activeKeys = _tokenStates
            .Where(kvp => !kvp.Value.IsCancelled)
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(activeKeys);
    }

    /// <summary>
    /// Cancel multiple cancellation tokens in batches
    /// </summary>
    /// <param name="keys">List of cancellation token keys to cancel</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public async Task CancelTokensAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null || keys.Count == 0)
            return;

        var cancelTasks = keys.Select(key => CancelTokenAsync(key, cancellationToken));
        await Task.WhenAll(cancelTasks);

        logger.LogInformation("Cancelled {Count} tokens", keys.Count);
    }

    /// <summary>
    /// Memory Token State Data Model
    /// </summary>
    private class InMemoryTokenState
    {
        /// <summary>
        /// cancel token key
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Has it been cancelled?
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// creation time
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Last updated
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Version number, used for status change tracking
        /// </summary>
        public long Version { get; set; } = 1;
    }
} 