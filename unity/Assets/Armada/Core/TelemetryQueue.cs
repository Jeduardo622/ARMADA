using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

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
        private readonly Queue<(string Json, int Bytes)> _events = new();
        private readonly object _gate = new();
        private readonly JsonSerializerSettings _options;
        private readonly int _maxPayloadBytes;
        private int _bytes;
        public TelemetryQueue(JsonSerializerSettings options, int maxPayloadBytes)
        { _options = options; _maxPayloadBytes = maxPayloadBytes; }
        public int Count { get { lock (_gate) return _events.Count; } }
        public bool Enqueue(TelemetryEvent evt)
        {
            if (evt == null) return false;
            try
            {
                var json = JsonConvert.SerializeObject(evt, _options);
                var bytes = Encoding.UTF8.GetByteCount(json);
                lock (_gate)
                {
                    if (bytes > _maxPayloadBytes || _events.Count >= 256 || _bytes + bytes > 256_000) return false;
                    _events.Enqueue((json, bytes)); _bytes += bytes;
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
        public List<TelemetryEvent> DequeueBatch(int max)
        {
            var list = new List<TelemetryEvent>();
            lock (_gate)
            {
                while (list.Count < max && _events.Count > 0)
                {
                    var item = _events.Dequeue(); _bytes -= item.Bytes;
                    list.Add(JsonConvert.DeserializeObject<TelemetryEvent>(item.Json, _options));
                }
            }
            return list;
        }
    }
}
