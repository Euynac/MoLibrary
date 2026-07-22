using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using Monica.DataChannel.Models;

namespace Monica.DataChannel.Middlewares
{
    public sealed class MessageReceiveDiagnosticsMiddleware(
        MessageReceiveDiagnostics messageReceiveDiagnostics
        ) : IEndpointFilter
    {
        private const string SnapshotItemKey = "Monica.DataChannel.MessageReceiveDiagnostics";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var snapshot = messageReceiveDiagnostics.CaptureSnapshot(context.HttpContext);
            context.HttpContext.Items[SnapshotItemKey] = snapshot;

            try
            {
                return await next(context);
            }
            finally
            {
                messageReceiveDiagnostics.Exit(snapshot.ReceiveId);
            }
        }

        public static MessageReceiveDiagnosticsSnapshot? GetSnapshot(HttpContext context)
        {
            return context.Items.TryGetValue(SnapshotItemKey, out var snapshot)
                ? snapshot as MessageReceiveDiagnosticsSnapshot
                : null;
        }
    }

    public sealed class MessageReceiveDiagnostics(string? route)
    {
        private long _receiveSequence;
        private readonly ConcurrentDictionary<string, MessageReceiveActive> _activeReceives = new();
        private readonly object _rateLock = new();
        private int _currentSecondMessageCount;
        private long _currentUnixTimeSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private int _lastSecondCompletedMessageCount;
        private long _maxConcurrentReceiveCount;
        private long _totalReceivedMessageCount;

        public MessageReceiveDiagnosticsSnapshot CaptureSnapshot(HttpContext context)
        {
            var utcNow = DateTimeOffset.UtcNow;
            var receiveId = $"{context.TraceIdentifier}:{Interlocked.Increment(ref _receiveSequence)}";
            var activeReceive = new MessageReceiveActive(
                receiveId,
                context.TraceIdentifier,
                Thread.CurrentThread.ManagedThreadId,
                utcNow);

            _activeReceives[receiveId] = activeReceive;

            var rate = CaptureRateSnapshot(utcNow);
            var activeCount = _activeReceives.Count;
            var maxConcurrentCount = UpdateMaxConcurrentReceiveCount(activeCount);
            var totalReceivedCount = Interlocked.Increment(ref _totalReceivedMessageCount);

            return new MessageReceiveDiagnosticsSnapshot(
                receiveId,
                GetRoute(context),
                context.TraceIdentifier,
                activeCount > 1,
                activeCount,
                maxConcurrentCount,
                rate.CurrentSecondMessageCount,
                rate.LastSecondCompletedMessageCount,
                totalReceivedCount,
                activeReceive.ThreadId,
                utcNow
                );
        }

        public void Exit(string receivedId)
        {
            _activeReceives.TryRemove(receivedId, out _);
        }

        private MessageReceiveRateSnapshot CaptureRateSnapshot(DateTimeOffset now)
        {
            lock (_rateLock)
            {
                var unixTimeSecond = now.ToUnixTimeSeconds();
                if (unixTimeSecond != _currentUnixTimeSecond)
                {
                    _lastSecondCompletedMessageCount = unixTimeSecond == _currentUnixTimeSecond + 1 ? _currentSecondMessageCount : 0;
                    _currentUnixTimeSecond = unixTimeSecond;
                    _currentSecondMessageCount = 0;
                }

                _currentSecondMessageCount++;

                return new MessageReceiveRateSnapshot(
                    _currentSecondMessageCount,
                    _lastSecondCompletedMessageCount
                    );
            }
        }

        private long UpdateMaxConcurrentReceiveCount(long activeCount)
        {
            while (true)
            {
                var max = Interlocked.Read(ref _maxConcurrentReceiveCount);
                if(activeCount <= max)
                {
                    return max;
                }

                if(Interlocked.CompareExchange(ref _maxConcurrentReceiveCount, activeCount, max) == max)
                {
                    return activeCount;
                }
            }
        }

        private string GetRoute(HttpContext context)
        {
            return string.IsNullOrWhiteSpace(route) ? context.Request.Path.ToString() : route;
        }
    }

}
