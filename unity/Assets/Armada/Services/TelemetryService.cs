using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class TelemetryService : IDisposable
    {
        private readonly ApiClient _client;
        private readonly TelemetryQueue _queue;
        private readonly float _flushSeconds;
        private readonly int _maxBatch;
        private readonly int _maxPayloadBytes;
        private readonly string _playerId;
        private readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
        private CancellationTokenSource _cts;

        public TelemetryService(ApiClient client, TelemetryQueue queue, float flushSeconds, int maxBatch, int maxPayloadBytes, string playerId)
        {
            _client = client;
            _queue = queue;
            _flushSeconds = flushSeconds;
            _maxBatch = maxBatch;
            _maxPayloadBytes = maxPayloadBytes;
            _playerId = playerId;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = FlushLoopAsync(_cts.Token);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Enqueue(string type, Dictionary<string, object> data)
        {
            var evt = new TelemetryEvent
            {
                Type = type,
                TimestampUtc = DateTime.UtcNow,
                Data = data ?? new Dictionary<string, object>()
            };

            if (!_queue.Enqueue(evt))
            {
                Debug.LogWarning($"[Telemetry] Dropped event {type} due to size");
            }
        }

        public Task FlushNowAsync() => FlushAsync();

        private async Task FlushLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_flushSeconds), token).ConfigureAwait(false);
                await FlushAsync().ConfigureAwait(false);
            }
        }

        private async Task FlushAsync()
        {
            if (_queue.Count == 0 || string.IsNullOrWhiteSpace(_playerId))
            {
                return;
            }

            var batch = _queue.DequeueBatch(_maxBatch);
            if (batch.Count == 0) return;

            var payload = new TelemetryIngestRequest
            {
                PlayerId = _playerId,
                Payload = new Dictionary<string, object>
                {
                    { "events", batch }
                }
            };

            // Keep under payload size guard.
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var byteCount = Encoding.UTF8.GetByteCount(json);
            while (byteCount > _maxPayloadBytes && batch.Count > 1)
            {
                var trimmed = batch[^1];
                batch.RemoveAt(batch.Count - 1);
                _queue.Enqueue(trimmed);
                payload.Payload["events"] = batch;
                json = JsonConvert.SerializeObject(payload, _jsonSettings);
                byteCount = Encoding.UTF8.GetByteCount(json);
            }

            if (byteCount > _maxPayloadBytes)
            {
                Debug.LogWarning("[Telemetry] Single event payload exceeds size guard; dropping.");
                return;
            }

            var resp = await _client.SendAsync<Dictionary<string, string>>("/telemetry/ingest", UnityWebRequest.kHttpVerbPOST, payload);
            if (!resp.Success)
            {
                Debug.LogWarning($"[Telemetry] Failed to send batch ({resp.StatusCode}). Will retry next flush.");
                // simple retry: push back into queue
                foreach (var evt in batch)
                {
                    _queue.Enqueue(evt);
                }
            }
            else
            {
                Debug.Log($"[Telemetry] Sent batch size {batch.Count}");
            }
        }
    }
}

