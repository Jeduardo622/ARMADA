using System;
using System.Collections.Generic;
using Armada.Client.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Armada.Client.Tests.EditMode
{
    public sealed class ArmadaEditModeTests
    {
        [Test]
        public void FeatureFlags_AreCaseInsensitiveAndRespectFallbacks()
        {
            var flags = new FeatureFlags(new[]
            {
                new FeatureToggleSetting { key = "missions", enabled = true }
            });

            Assert.That(flags.IsEnabled("MISSIONS"), Is.True);
            Assert.That(flags.IsEnabled("inventory"), Is.False);
            Assert.That(flags.IsEnabled("inventory", true), Is.True);

            flags.DisableFromForbidden("Missions");
            Assert.That(flags.IsEnabled("missions"), Is.False);
        }

        [Test]
        public void TelemetryQueue_RejectsOversizedEventsAndPreservesOrder()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var queue = new TelemetryQueue(settings, 4096);
            var first = Event("first", "one");
            var second = Event("second", "two");

            Assert.That(queue.Enqueue(first), Is.True);
            Assert.That(queue.Enqueue(second), Is.True);
            Assert.That(queue.DequeueBatch(2), Is.EqualTo(new[] { first, second }));

            var bounded = new TelemetryQueue(settings, 32);
            Assert.That(bounded.Enqueue(Event("oversized", new string('x', 128))), Is.False);
            Assert.That(bounded.Count, Is.Zero);
        }

        [Test]
        public void DeterministicSimHooks_ReapplyingASeedRepeatsTheSequence()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("determinism-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();
                hooks.ApplySeed(20260710);
                var first = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                var second = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

                hooks.ApplySeed(20260710);
                Assert.That(UnityEngine.Random.Range(int.MinValue, int.MaxValue), Is.EqualTo(first));
                Assert.That(UnityEngine.Random.Range(int.MinValue, int.MaxValue), Is.EqualTo(second));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SimPreviewRequest_UsesBackendSchemaKeys()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var request = new SimPreviewRequest
            {
                Seed = 42,
                State = new SimState { Ships = new List<SimShip>() },
                Orders = new List<SimOrder>()
            };

            var json = JsonConvert.SerializeObject(request, settings);

            StringAssert.Contains("\"schemaVersion\":1", json);
            StringAssert.Contains("\"seed\":42", json);
            StringAssert.Contains("\"orders\":[]", json);
        }

        private static TelemetryEvent Event(string type, string value)
        {
            return new TelemetryEvent
            {
                Type = type,
                TimestampUtc = DateTime.UnixEpoch,
                Data = new Dictionary<string, object> { ["value"] = value }
            };
        }
    }
}
