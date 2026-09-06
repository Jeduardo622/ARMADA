using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Armada.Client.Tests
{
    public sealed class CampaignTelemetryTests
    {
        [Test]
        public void Envelope_OmitsAbsentOptionalFieldsAndPreservesMissionScope()
        {
            var settings = new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
            var request = new TelemetryIngestRequest { Payload = new Dictionary<string, object>() };
            var json = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(request, settings));
            Assert.That(json.Property("missionCode"), Is.Null);
            Assert.That(json.Property("playerId"), Is.Null);
            request.PlayerId = "owner"; request.MissionCode = "mission_01";
            json = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(request, settings));
            Assert.That((string)json["playerId"], Is.EqualTo("owner"));
            Assert.That((string)json["missionCode"], Is.EqualTo("mission_01"));
        }

        [Test]
        public void Queue_BoundsOfflineMemoryAndFreezesAcceptedEvents()
        {
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 1024);
            var data = new Dictionary<string, object> { ["value"] = "original" };
            queue.Enqueue(new TelemetryEvent { Type = "first", Data = data });
            data["value"] = "changed";
            for (var i = 0; i < 1000; i++) queue.Enqueue(new TelemetryEvent { Type = "other" });
            Assert.That(queue.Count, Is.LessThanOrEqualTo(256));
            Assert.That(queue.DequeueBatch(1)[0].Data["value"].ToString(), Is.EqualTo("original"));
        }
        [Test]
        public void Queue_DoesNotThrowOnUnserializableTelemetry()
        {
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 1024);
            var data = new Dictionary<string, object>(); data["cycle"] = data;
            Assert.That(queue.Enqueue(new TelemetryEvent { Type = "bad", Data = data }), Is.False);
        }

        [Test]
        public void Flush_SharesPendingSendAndSwallowsFailureForBoundedRetry()
        {
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 10000);
            var pending = new TaskCompletionSource<bool>();
            var transport = new Transport { Send = _ => pending.Task };
            using var service = new TelemetryService(transport, queue, 5, 25, 10000, "owner");
            service.Enqueue("session_start", null);
            var first = service.FlushNowAsync();
            var second = service.FlushNowAsync();
            Assert.That(second, Is.SameAs(first));
            Assert.That(transport.Calls, Is.EqualTo(1));
            pending.SetException(new InvalidOperationException("must not escape"));
            first.GetAwaiter().GetResult();
            Assert.That(queue.Count, Is.EqualTo(1));
            transport.Send = _ => Task.FromResult(true);
            service.FlushNowAsync().GetAwaiter().GetResult();
            Assert.That(queue.Count, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_DoesNotResendOrRequeueAnInFlightFailure()
        {
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 10000);
            var pending = new TaskCompletionSource<bool>();
            var transport = new Transport { Send = _ => pending.Task };
            var service = new TelemetryService(transport, queue, 5, 25, 10000, "owner");
            service.Enqueue("session_start", null);
            var flush = service.FlushNowAsync();
            service.Dispose(); service.Dispose(); service.Start();
            pending.SetResult(false); flush.GetAwaiter().GetResult();
            service.Enqueue("ignored", null); service.FlushNowAsync().GetAwaiter().GetResult();
            Assert.That(queue.Count, Is.Zero);
            Assert.That(transport.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Loop_StartsOnceResumesOnUnityThreadAndCancelsCleanly()
        {
            var thread = Thread.CurrentThread.ManagedThreadId;
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 10000);
            var delays = new List<TaskCompletionSource<bool>>();
            var transportThread = 0;
            var transport = new Transport { Send = _ => { transportThread = Thread.CurrentThread.ManagedThreadId; return Task.FromResult(true); } };
            Task Delay(TimeSpan _, CancellationToken token)
            {
                var gate = new TaskCompletionSource<bool>();
                token.Register(() => gate.TrySetCanceled()); delays.Add(gate); return gate.Task;
            }
            using var service = new TelemetryService(transport, queue, 5, 25, 10000, "owner", Delay);
            service.Enqueue("session_start", null); service.Start(); service.Start();
            Assert.That(delays.Count, Is.EqualTo(1));
            var completion = Task.Run(() => delays[0].TrySetResult(true));
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while ((!completion.IsCompleted || transport.Calls == 0) && deadline.Elapsed < TimeSpan.FromSeconds(15)) yield return null;
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(transportThread, Is.EqualTo(thread));
            service.Dispose();
            deadline.Restart();
            while (!service.Completion.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(15)) yield return null;
            Assert.That(service.Completion.IsCompleted, Is.True);
            Assert.That(service.Completion.IsFaulted, Is.False);
        }

        [Test]
        public void CampaignEvents_UseAllowlistedCoarseDataAndNeverExportRawReasons()
        {
            var queue = new TelemetryQueue(new JsonSerializerSettings(), 10000);
            using var service = new TelemetryService(new Transport(), queue, 5, 25, 10000, null);
            var events = new CampaignTelemetry(service);
            const string secret = "raw-credential-device-error";
            events.SessionStart(secret, 16000); events.SessionStart("android", 4000);
            events.MissionStart(1); events.MissionEnd(1, false, secret, 99, 3); events.MissionEnd(1, false, secret, 99, 3);
            events.Performance(57, 17); events.Economy(true, "gold", 100, "mission_clear");
            events.Economy(false, secret, 1, secret); events.SailImpression(secret); events.SailClick("harbor_blue");
            events.SessionEnd(600); events.SessionEnd(600);
            var captured = queue.DequeueBatch(256);
            Assert.That(captured.Count, Is.EqualTo(8));
            var json = JsonConvert.SerializeObject(captured);
            Assert.That(json, Does.Not.Contain(secret));
            Assert.That(json, Does.Contain("other"));
            Assert.That(json, Does.Contain("store_impression"));
            Assert.DoesNotThrow(() => new CampaignTelemetry(null).SessionEnd(double.NaN));
        }

        private sealed class Transport : ITelemetryTransport
        {
            public int Calls;
            public Func<TelemetryIngestRequest, Task<bool>> Send = _ => Task.FromResult(true);
            public Task<bool> SendAsync(TelemetryIngestRequest request) { Calls++; return Send(request); }
        }
    }
}
