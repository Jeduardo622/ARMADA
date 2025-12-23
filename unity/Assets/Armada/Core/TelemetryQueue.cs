using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Armada.Client.Core
{
    public sealed class TelemetryEvent
    {
        public string Type;
        public DateTime TimestampUtc;
        public Dictionary<string, object> Data;
    }

    public sealed class TelemetryQueue
    {
        private readonly Queue<TelemetryEvent> _events = new();
        private readonly JsonSerializerOptions _options;
        private readonly int _maxPayloadBytes;

        public TelemetryQueue(JsonSerializerOptions options, int maxPayloadBytes)
        {
            _options = options;
            _maxPayloadBytes = maxPayloadBytes;
        }

        public int Count => _events.Count;

        public bool Enqueue(TelemetryEvent evt)
        {
            if (evt == null) return false;
            var asJson = JsonSerializer.Serialize(evt, _options);
            var size = Encoding.UTF8.GetByteCount(asJson);
            if (size > _maxPayloadBytes)
            {
                return false;
            }

            _events.Enqueue(evt);
            return true;
        }

        public List<TelemetryEvent> DequeueBatch(int max)
        {
            var list = new List<TelemetryEvent>(Math.Min(max, _events.Count));
            while (list.Count < max && _events.Count > 0)
            {
                list.Add(_events.Dequeue());
            }

            return list;
        }
    }
}

