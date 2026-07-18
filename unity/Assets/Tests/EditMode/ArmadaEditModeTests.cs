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

        [Test]
        public void Mission01Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission01.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-01-fair-wind|turnLimit=8|bonusTurns=6|bonusHull=0.2|enemyScale=0.9|wind=0:5|" +
                "enemy-sloop:enemy:150,0:h180:v2:hp108:sl70:cw40|player-sloop:player:0,0:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission01Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission01Scenario.FingerprintOf(Mission01Scenario.BuildExpectedStart(44)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission02Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission02.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-02-weather-gage|turnLimit=9|bonusTurns=7|upwindTurns=3|enemyScale=1|wind=90:5|" +
                "island=100,40:r25|" +
                "enemy-aggressor:enemy:170,120:h215:v2:hp120:sl70:cw40|" +
                "enemy-kite:enemy:220,160:h215:v2:hp120:sl70:cw40|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission02Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission02Scenario.FingerprintOf(Mission02Scenario.BuildExpectedStart(202)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission03Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission03.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-03-raking-shot|turnLimit=10|bonusTurns=8|rakeTarget=2|enemyScale=1.05|wind=90:3|" +
                "enemy-frigate:enemy:200,90:h205:v2:hp189:sl90:cw60|" +
                "enemy-sloop:enemy:200,-90:h155:v3:hp126:sl70:cw40|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission03Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission03Scenario.FingerprintOf(Mission03Scenario.BuildExpectedStart(303)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission04Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission04.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-04-boarding-party|turnLimit=10|crewScale=0.9|boardBonus=0.1|wind=180:3|" +
                "debris=130,0:r45:p2|" +
                "enemy-frigate-a:enemy:220,40:h180:v2:hp180:sl90:cw54|" +
                "enemy-frigate-b:enemy:260,-40:h180:v2:hp180:sl90:cw54|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission04Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission04Scenario.FingerprintOf(Mission04Scenario.BuildExpectedStart(404)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission05Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission05.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-05-line-break|turnLimit=11|bonusTurns=9|flagshipScale=1.1|wind=0:5|" +
                "rock=120,70:r35|rock=120,-70:r35|" +
                "enemy-escort-a:enemy:240,60:h180:v2:hp120:sl70:cw40|" +
                "enemy-escort-b:enemy:240,-60:h180:v2:hp120:sl70:cw40|" +
                "enemy-flagship:enemy:260,0:h180:v2:hp198:sl90:cw60|" +
                "player-sloop-a:player:0,50:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,0:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-c:player:0,-50:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission05Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission05Scenario.FingerprintOf(Mission05Scenario.BuildExpectedStart(505)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission06Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission06.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-06-dreadnought-siege|turnLimit=14|bonusTurns=12|bossScale=1.3|bossDmg=1.1|" +
                "enrage=0.3|reinforce=5:0.9|wind=0:5|debris=150,0:r50:p2|" +
                "enemy-dreadnought:enemy:280,0:h180:v2:hp468:sl100:cw80|" +
                "player-sloop-a:player:0,50:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,0:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-c:player:0,-50:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission06Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission06Scenario.FingerprintOf(Mission06Scenario.BuildExpectedStart(606)),
                Is.EqualTo(expected));
        }

        [Test]
        public void MissionCompleteResponse_DeserializesBackendPayload()
        {
            // Mirrors the /missions/{code}/complete response contract in
            // docs/api/openapi.yaml (camelCase, rewardsGranted sibling of progress).
            const string json =
                "{\"progress\":{\"playerId\":\"11111111-1111-1111-1111-111111111111\"," +
                "\"missionId\":\"22222222-2222-2222-2222-222222222222\",\"status\":\"COMPLETED\",\"bestScore\":42}," +
                "\"rewardsGranted\":[{\"itemKey\":\"gold\",\"quantity\":100},{\"itemKey\":\"timber\",\"quantity\":50}]}";

            var response = JsonConvert.DeserializeObject<MissionCompleteResponse>(json);

            Assert.That(response.Progress.Status, Is.EqualTo("COMPLETED"));
            Assert.That(response.Progress.BestScore, Is.EqualTo(42));
            Assert.That(response.RewardsGranted, Has.Count.EqualTo(2));
            Assert.That(response.RewardsGranted[0].ItemKey, Is.EqualTo("gold"));
            Assert.That(response.RewardsGranted[0].Quantity, Is.EqualTo(100));
            Assert.That(response.RewardsGranted[1].ItemKey, Is.EqualTo("timber"));
            Assert.That(response.RewardsGranted[1].Quantity, Is.EqualTo(50));
        }

        [Test]
        public void SimStatusEvent_DeserializesStatusEffectCounters()
        {
            // Mirrors the SimShipStatus schema in docs/api/openapi.yaml: the
            // booleans stay the wire truth and the remaining-turn counters
            // added by the status-effects slice are optional.
            const string json =
                "{\"type\":\"status\",\"shipId\":\"player-sloop\"," +
                "\"status\":{\"onFire\":true,\"fireTurnsRemaining\":2,\"slowed\":true,\"slowTurnsRemaining\":1}}";

            var statusEvent = JsonConvert.DeserializeObject<SimEvent>(json);

            Assert.That(statusEvent.Type, Is.EqualTo("status"));
            Assert.That(statusEvent.ShipId, Is.EqualTo("player-sloop"));
            Assert.That(statusEvent.Status.OnFire, Is.True);
            Assert.That(statusEvent.Status.FireTurnsRemaining, Is.EqualTo(2));
            Assert.That(statusEvent.Status.Slowed, Is.True);
            Assert.That(statusEvent.Status.SlowTurnsRemaining, Is.EqualTo(1));

            var cleared = JsonConvert.DeserializeObject<SimShipStatus>("{}");
            Assert.That(cleared.OnFire, Is.Null);
            Assert.That(cleared.FireTurnsRemaining, Is.Null);
            Assert.That(cleared.SlowTurnsRemaining, Is.Null);
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
