using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Armada.Client.Core;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public interface ITelemetryTransport
    {
        Task<bool> SendAsync(TelemetryIngestRequest request);
    }

    /// <summary>Start and flush on the Unity thread. All awaits preserve its context.</summary>
    public sealed class TelemetryService : IDisposable
    {
        private readonly ITelemetryTransport _transport;
        private readonly TelemetryQueue _queue;
        private readonly TimeSpan _interval;
        private readonly int _maxBatch;
        private readonly int _maxPayloadBytes;
        private readonly string _playerId;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly JsonSerializerSettings _json = new() { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
        private readonly CancellationTokenSource _lifetime = new();
        private readonly TaskCompletionSource<bool> _stopped = new();
        private Task _loop;
        private Task _flush;
        private bool _disposed;
        public Task Completion => _loop ?? Task.CompletedTask;

        public TelemetryService(ApiClient client, TelemetryQueue queue, float flushSeconds, int maxBatch, int maxPayloadBytes, string playerId)
            : this(new ApiTransport(client), queue, flushSeconds, maxBatch, maxPayloadBytes, playerId) { }
        public TelemetryService(ITelemetryTransport transport, TelemetryQueue queue, float flushSeconds, int maxBatch, int maxPayloadBytes,
            string playerId, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _transport = transport; _queue = queue;
            _interval = TimeSpan.FromSeconds(float.IsNaN(flushSeconds) || float.IsInfinity(flushSeconds) ? 5 : Math.Max(.1f, flushSeconds));
            _maxBatch = Math.Max(1, Math.Min(256, maxBatch)); _maxPayloadBytes = Math.Max(1, Math.Min(10_000, maxPayloadBytes));
            _playerId = playerId; _delay = delay ?? Task.Delay;
        }
        public void Start()
        {
            if (_disposed || _loop != null) return;
            _loop = FlushLoopAsync(_lifetime.Token);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _stopped.TrySetResult(true); _lifetime.Cancel(); _lifetime.Dispose();
        }
        public void Enqueue(string type, Dictionary<string, object> data)
        {
            if (_disposed) return;
            _queue.Enqueue(new TelemetryEvent { Type = type, TimestampUtc = DateTime.UtcNow, Data = data ?? new Dictionary<string, object>() });
        }
        public Task FlushNowAsync()
        {
            if (_disposed) return Task.CompletedTask;
            if (_flush != null) return _flush;
            var pending = FlushAsync();
            if (!pending.IsCompleted) _flush = pending;
            return pending;
        }
        private async Task FlushLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await _delay(_interval, token);
                    if (token.IsCancellationRequested) break;
                    // ApiClient has no request-abort seam. Stop waiting for an
                    // in-flight POST on disposal; its observed completion cannot
                    // requeue or start another send after this service stops.
                    await Task.WhenAny(FlushNowAsync(), _stopped.Task);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Telemetry must not escape into the game loop. */ }
        }
        private async Task FlushAsync()
        {
            List<TelemetryEvent> batch = null;
            try
            {
                if (_queue.Count == 0 || string.IsNullOrWhiteSpace(_playerId)) return;
                batch = _queue.DequeueBatch(_maxBatch);
                var payload = new TelemetryIngestRequest { PlayerId = _playerId,
                    Payload = new Dictionary<string, object> { ["events"] = batch } };
                while (Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(payload, _json)) > _maxPayloadBytes)
                {
                    if (batch.Count <= 1) { batch.Clear(); return; }
                    var trimmed = batch[batch.Count - 1]; batch.RemoveAt(batch.Count - 1); _queue.Enqueue(trimmed);
                }
                if (_disposed) return;
                if (await _transport.SendAsync(payload)) batch = null;
            }
            catch (Exception) { /* No raw errors or payloads are logged. */ }
            finally
            {
                if (!_disposed && batch != null) foreach (var evt in batch) _queue.Enqueue(evt);
                _flush = null;
            }
        }
        private sealed class ApiTransport : ITelemetryTransport
        {
            private readonly ApiClient _client;
            public ApiTransport(ApiClient client) { _client = client; }
            public async Task<bool> SendAsync(TelemetryIngestRequest request) =>
                (await _client.SendAsync<Dictionary<string, string>>("/telemetry/ingest", UnityWebRequest.kHttpVerbPOST, request)).Success;
        }
    }
}
