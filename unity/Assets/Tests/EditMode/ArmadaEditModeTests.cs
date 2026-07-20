using System;
using System.Collections.Generic;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
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
        public void SimPreviewRequest_SerializesUpgradesWithBackendKeysAndOmitsWhenNull()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var withUpgrades = new SimPreviewRequest
            {
                Seed = 7,
                State = new SimState { Ships = new List<SimShip>() },
                Orders = new List<SimOrder>(),
                Modifiers = new SimModifiers { ShipUpgrades = true },
                Upgrades = new SimShipUpgrades { Cannon = 3, Sail = 1, Hull = 2 }
            };

            var json = JsonConvert.SerializeObject(withUpgrades, settings);

            StringAssert.Contains("\"modifiers\":{\"shipUpgrades\":true}", json);
            StringAssert.Contains("\"upgrades\":{\"cannon\":3,\"sail\":1,\"hull\":2}", json);

            // Flag-off requests must stay byte-identical to the legacy payload.
            var withoutUpgrades = new SimPreviewRequest
            {
                Seed = 7,
                State = new SimState { Ships = new List<SimShip>() },
                Orders = new List<SimOrder>()
            };

            var legacyJson = JsonConvert.SerializeObject(withoutUpgrades, settings);

            StringAssert.DoesNotContain("modifiers", legacyJson);
            StringAssert.DoesNotContain("upgrades", legacyJson);
        }

        [Test]
        public void MissionRequests_SerializeUpgradeTiersWithBackendKeysAndOmitWhenNull()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var resolve = new Mission01ResolveRequest
            {
                Seed = 21,
                Turns = new List<List<SimOrder>>(),
                Upgrades = new SimShipUpgrades { Cannon = 3, Sail = 3, Hull = 3 }
            };
            var completeRequest = new MissionCompleteRequest
            {
                PlayerId = "11111111-1111-1111-1111-111111111111",
                Seed = 21,
                Turns = new List<List<SimOrder>>(),
                Upgrades = new SimShipUpgrades { Cannon = 2, Sail = 0, Hull = 1 }
            };

            StringAssert.Contains(
                "\"upgrades\":{\"cannon\":3,\"sail\":3,\"hull\":3}",
                JsonConvert.SerializeObject(resolve, settings));
            StringAssert.Contains(
                "\"upgrades\":{\"cannon\":2,\"sail\":0,\"hull\":1}",
                JsonConvert.SerializeObject(completeRequest, settings));

            // Requests without tiers must stay byte-identical to the legacy
            // payloads accepted by the strict mission schemas.
            var legacyResolve = new Mission01ResolveRequest
            {
                Seed = 21,
                Turns = new List<List<SimOrder>>()
            };
            StringAssert.DoesNotContain(
                "upgrades",
                JsonConvert.SerializeObject(legacyResolve, settings));
        }

        [Test]
        public void Mission07Flow_MapsOwnedTiersToRequestUpgradesAndOmitsWhenNothingOwned()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var resolve = new Mission01ResolveRequest
            {
                Seed = 21,
                Turns = new List<List<SimOrder>>(),
                Upgrades = Mission07Flow.MapOwnedTiers(new List<OwnedUpgrade>
                {
                    new OwnedUpgrade { Component = "cannon", Tier = 3 },
                    new OwnedUpgrade { Component = "sail", Tier = 1 },
                    new OwnedUpgrade { Component = "hull", Tier = 2 }
                })
            };

            StringAssert.Contains(
                "\"upgrades\":{\"cannon\":3,\"sail\":1,\"hull\":2}",
                JsonConvert.SerializeObject(resolve, settings));

            // Nothing owned maps to null, so the serialized request stays
            // byte-identical to the legacy payload.
            var unowned = new Mission01ResolveRequest
            {
                Seed = 21,
                Turns = new List<List<SimOrder>>(),
                Upgrades = Mission07Flow.MapOwnedTiers(new List<OwnedUpgrade>
                {
                    new OwnedUpgrade { Component = "cannon", Tier = 0 },
                    new OwnedUpgrade { Component = "sail", Tier = 0 },
                    new OwnedUpgrade { Component = "hull", Tier = 0 }
                })
            };

            StringAssert.DoesNotContain(
                "upgrades",
                JsonConvert.SerializeObject(unowned, settings));
            Assert.That(Mission07Flow.MapOwnedTiers(null), Is.Null);
            Assert.That(Mission07Flow.MapOwnedTiers(new List<OwnedUpgrade>()), Is.Null);
        }

        [Test]
        public void Mission07Bootstrap_BuildsPinnedGunneryOrdersForDefaultSeed()
        {
            // Seed 21 and the pure-gunnery orders are the deterministic win
            // fixture pinned in tests/mission07.test.ts; the bootstrap must
            // mirror them exactly or the runtime run stops winning.
            Assert.That(Armada.Client.Bootstrap.Mission07Bootstrap.DefaultSeed, Is.EqualTo(21));

            var turns = Armada.Client.Bootstrap.Mission07Bootstrap.BuildGunneryOrders();
            Assert.That(turns, Has.Count.EqualTo(Mission07Scenario.TurnLimit));

            for (var i = 0; i < turns.Count; i++)
            {
                var expectedTarget = i < 5 ? Mission07Scenario.EnemyShipIds[0] : Mission07Scenario.EnemyShipIds[1];
                var expectedSpeedDelta = i >= 3 ? -2 : 0;

                Assert.That(turns[i], Has.Count.EqualTo(2));
                for (var ship = 0; ship < 2; ship++)
                {
                    var order = turns[i][ship];
                    Assert.That(order.ShipId, Is.EqualTo(Mission07Scenario.PlayerShipIds[ship]));
                    Assert.That(order.Action, Is.EqualTo("broadside"));
                    Assert.That(order.TargetShipId, Is.EqualTo(expectedTarget));
                    Assert.That(order.Side, Is.EqualTo("starboard"));
                    Assert.That(order.TurnDelta, Is.Zero);
                    Assert.That(order.SpeedDelta, Is.EqualTo(expectedSpeedDelta));
                }
            }
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
        public void Mission07Scenario_FingerprintMatchesBackendPin()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission07.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-07-burning-seas|turnLimit=10|sailScale=0.85|ignitionTarget=1|wind=0:4|" +
                "enemy-frigate-a:enemy:220,40:h180:v2:hp180:sl76:cw60|" +
                "enemy-frigate-b:enemy:220,-40:h180:v2:hp180:sl76:cw60|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission07Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission07Scenario.FingerprintOf(Mission07Scenario.BuildExpectedStart(707)),
                Is.EqualTo(expected));
        }

        [Test]
        public void Mission08Scenario_FingerprintMatchesBackendPinAndBootstrapMirrorsTackingOrders()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission08.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-08-eye-of-the-wind|turnLimit=10|upwindLimit=30|downwindLimit=90|swiftTarget=8|wind=180:4|" +
                "enemy-corvette-a:enemy:240,35:h180:v3:hp150:sl85:cw55|" +
                "enemy-corvette-b:enemy:240,-35:h180:v3:hp150:sl85:cw55|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission08Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission08Scenario.FingerprintOf(Mission08Scenario.BuildExpectedStart(808)),
                Is.EqualTo(expected));

            // Seed 9 and the tacking orders are the deterministic win fixture
            // pinned in tests/mission08.test.ts; the bootstrap must mirror
            // them exactly or the runtime run stops winning.
            Assert.That(Armada.Client.Bootstrap.Mission08Bootstrap.DefaultSeed, Is.EqualTo(9));

            var turns = Armada.Client.Bootstrap.Mission08Bootstrap.BuildTackingOrders();
            Assert.That(turns, Has.Count.EqualTo(Mission08Scenario.TurnLimit));

            for (var i = 0; i < turns.Count; i++)
            {
                var expectedTarget = i < 5 ? Mission08Scenario.EnemyShipIds[0] : Mission08Scenario.EnemyShipIds[1];
                var expectedTurnDelta = i == 1 ? 60 : i == 2 ? -60 : 0;
                var expectedSpeedDelta = i >= 3 ? -2 : 0;

                Assert.That(turns[i], Has.Count.EqualTo(2));
                for (var ship = 0; ship < 2; ship++)
                {
                    var order = turns[i][ship];
                    Assert.That(order.ShipId, Is.EqualTo(Mission08Scenario.PlayerShipIds[ship]));
                    Assert.That(order.Action, Is.EqualTo("broadside"));
                    Assert.That(order.TargetShipId, Is.EqualTo(expectedTarget));
                    Assert.That(order.Side, Is.EqualTo("starboard"));
                    Assert.That(order.TurnDelta, Is.EqualTo(expectedTurnDelta));
                    Assert.That(order.SpeedDelta, Is.EqualTo(expectedSpeedDelta));
                }
            }
        }

        [Test]
        public void Mission09Scenario_FingerprintMatchesBackendPinAndBootstrapMirrorsRammingOrders()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission09.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-09-iron-bow|turnLimit=10|ramRange=25|ramTarget=2|wind=0:4|" +
                "enemy-brig-a:enemy:220,35:h180:v3:hp160:sl85:cw55|" +
                "enemy-brig-b:enemy:220,-35:h180:v3:hp160:sl85:cw55|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission09Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission09Scenario.FingerprintOf(Mission09Scenario.BuildExpectedStart(909)),
                Is.EqualTo(expected));

            // Seed 87 and the ramming orders are the deterministic win fixture
            // pinned in tests/mission09.test.ts; the bootstrap must mirror
            // them exactly or the runtime run stops winning.
            Assert.That(Armada.Client.Bootstrap.Mission09Bootstrap.DefaultSeed, Is.EqualTo(87));

            var turns = Armada.Client.Bootstrap.Mission09Bootstrap.BuildRammingOrders();
            Assert.That(turns, Has.Count.EqualTo(Mission09Scenario.TurnLimit));

            for (var i = 0; i < turns.Count; i++)
            {
                var expectedTarget = i < 5 ? Mission09Scenario.EnemyShipIds[0] : Mission09Scenario.EnemyShipIds[1];
                var expectedSpeedDelta = i < 2 ? 2 : 0;

                Assert.That(turns[i], Has.Count.EqualTo(2));
                for (var ship = 0; ship < 2; ship++)
                {
                    var order = turns[i][ship];
                    Assert.That(order.ShipId, Is.EqualTo(Mission09Scenario.PlayerShipIds[ship]));
                    Assert.That(order.Action, Is.EqualTo("broadside"));
                    Assert.That(order.TargetShipId, Is.EqualTo(expectedTarget));
                    Assert.That(order.Side, Is.EqualTo("starboard"));
                    Assert.That(order.TurnDelta, Is.EqualTo(0));
                    Assert.That(order.SpeedDelta, Is.EqualTo(expectedSpeedDelta));
                }
            }

            // Mirrors the SimEvent "ram" variant in docs/api/openapi.yaml so
            // per-ram damage stays readable client-side (hullDamage vs
            // selfHullDamage plus both remaining blocks).
            const string ramJson =
                "{\"type\":\"ram\",\"shipId\":\"player-sloop-a\",\"targetShipId\":\"enemy-brig-a\"," +
                "\"effectiveSpeed\":9,\"hullDamage\":46,\"selfHullDamage\":23," +
                "\"targetRemaining\":{\"hp\":114,\"sail\":85,\"crew\":55}," +
                "\"rammerRemaining\":{\"hp\":97,\"sail\":80,\"crew\":50}}";

            var ramEvent = JsonConvert.DeserializeObject<SimEvent>(ramJson);
            Assert.That(ramEvent.Type, Is.EqualTo("ram"));
            Assert.That(ramEvent.ShipId, Is.EqualTo("player-sloop-a"));
            Assert.That(ramEvent.TargetShipId, Is.EqualTo("enemy-brig-a"));
            Assert.That(ramEvent.EffectiveSpeed, Is.EqualTo(9));
            Assert.That(ramEvent.HullDamage, Is.EqualTo(46));
            Assert.That(ramEvent.SelfHullDamage, Is.EqualTo(23));
            Assert.That(ramEvent.TargetRemaining.Hp, Is.EqualTo(114));
            Assert.That(ramEvent.RammerRemaining.Hp, Is.EqualTo(97));
            Assert.That(ramEvent.RammerRemaining.Sail, Is.EqualTo(80));
            Assert.That(ramEvent.RammerRemaining.Crew, Is.EqualTo(50));
        }

        [Test]
        public void Mission10Scenario_FingerprintMatchesBackendPinAndBootstrapMirrorsMixedBatteryOrders()
        {
            // Must equal EXPECTED_FINGERPRINT in tests/mission10.test.ts so the
            // client and server pin the identical deterministic scenario.
            const string expected =
                "mission-10-sail-cutter|turnLimit=10|chainHull=40|chainSail=120|chainCrew=20|sailTarget=60|wind=0:4|" +
                "enemy-clipper-a:enemy:220,35:h180:v3:hp140:sl110:cw50|" +
                "enemy-clipper-b:enemy:220,-35:h180:v3:hp140:sl110:cw50|" +
                "player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|" +
                "player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50";

            Assert.That(Mission10Scenario.Fingerprint(), Is.EqualTo(expected));
            Assert.That(
                Mission10Scenario.FingerprintOf(Mission10Scenario.BuildExpectedStart(1010)),
                Is.EqualTo(expected));

            // Seed 2 and the mixed-battery orders are the deterministic win
            // fixture pinned in tests/mission10.test.ts; the bootstrap must
            // mirror them exactly or the runtime run stops winning.
            Assert.That(Armada.Client.Bootstrap.Mission10Bootstrap.DefaultSeed, Is.EqualTo(2));

            var turns = Armada.Client.Bootstrap.Mission10Bootstrap.BuildMixedBatteryOrders();
            Assert.That(turns, Has.Count.EqualTo(Mission10Scenario.TurnLimit));

            for (var i = 0; i < turns.Count; i++)
            {
                var expectedTarget = i < 5 ? Mission10Scenario.EnemyShipIds[0] : Mission10Scenario.EnemyShipIds[1];
                var expectedAmmo = i < 3 ? "chain" : null;

                Assert.That(turns[i], Has.Count.EqualTo(2));
                for (var ship = 0; ship < 2; ship++)
                {
                    var order = turns[i][ship];
                    Assert.That(order.ShipId, Is.EqualTo(Mission10Scenario.PlayerShipIds[ship]));
                    Assert.That(order.Action, Is.EqualTo("broadside"));
                    Assert.That(order.TargetShipId, Is.EqualTo(expectedTarget));
                    Assert.That(order.Side, Is.EqualTo("starboard"));
                    Assert.That(order.TurnDelta, Is.EqualTo(0));
                    Assert.That(order.SpeedDelta, Is.EqualTo(0));
                    Assert.That(order.Ammo, Is.EqualTo(expectedAmmo));
                }
            }

            // Mirrors the SimEvent "broadside" variant with the chain-shot
            // marker in docs/api/openapi.yaml so ammo readability survives
            // Json.NET deserialization (unmapped fields drop silently).
            const string chainJson =
                "{\"type\":\"broadside\",\"shipId\":\"player-sloop-a\",\"targetShipId\":\"enemy-clipper-a\"," +
                "\"side\":\"starboard\",\"hit\":true,\"roll\":68,\"hitChance\":72," +
                "\"damage\":{\"hull\":11,\"sail\":33,\"crew\":5}," +
                "\"targetRemaining\":{\"hp\":89,\"sail\":47,\"crew\":35},\"ammo\":\"chain\"}";

            var chainEvent = JsonConvert.DeserializeObject<SimEvent>(chainJson);
            Assert.That(chainEvent.Type, Is.EqualTo("broadside"));
            Assert.That(chainEvent.Ammo, Is.EqualTo("chain"));
            Assert.That(chainEvent.Hit, Is.True);
            Assert.That(chainEvent.Damage.Hull, Is.EqualTo(11));
            Assert.That(chainEvent.Damage.Sail, Is.EqualTo(33));
            Assert.That(chainEvent.Damage.Crew, Is.EqualTo(5));
            Assert.That(chainEvent.TargetRemaining.Hp, Is.EqualTo(89));

            // A null ammo must be omitted on the wire so round-shot and
            // legacy-mission order payloads stay byte-identical to the
            // pre-chain-shot shape (SimModifiers precedent).
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var roundOrder = new SimOrder
            {
                ShipId = "player-sloop-a",
                Action = "broadside",
                TargetShipId = "enemy-clipper-a",
                Side = "starboard",
                TurnDelta = 0,
                SpeedDelta = 0
            };
            Assert.That(JsonConvert.SerializeObject(roundOrder, settings), Does.Not.Contain("ammo"));
            var chainOrder = new SimOrder
            {
                ShipId = "player-sloop-a",
                Action = "broadside",
                TargetShipId = "enemy-clipper-a",
                Side = "starboard",
                TurnDelta = 0,
                SpeedDelta = 0,
                Ammo = "chain"
            };
            Assert.That(JsonConvert.SerializeObject(chainOrder, settings), Does.Contain("\"ammo\":\"chain\""));
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
        public void UpgradeResponses_DeserializeBackendPayloads()
        {
            // Mirrors the /upgrades and /upgrades/purchase response contracts
            // in docs/api/openapi.yaml (camelCase; catalog tiers carry costs,
            // purchase returns the upgrade with the spent costs).
            const string listJson =
                "{\"catalog\":[{\"component\":\"cannon\",\"tiers\":[" +
                "{\"tier\":1,\"costs\":[{\"itemKey\":\"gold\",\"quantity\":100},{\"itemKey\":\"ore\",\"quantity\":20}]}]}]," +
                "\"owned\":[{\"component\":\"cannon\",\"tier\":1},{\"component\":\"sail\",\"tier\":0}]}";
            const string purchaseJson =
                "{\"upgrade\":{\"playerId\":\"11111111-1111-1111-1111-111111111111\"," +
                "\"component\":\"cannon\",\"tier\":2}," +
                "\"spent\":[{\"itemKey\":\"gold\",\"quantity\":250},{\"itemKey\":\"ore\",\"quantity\":50}]}";

            var list = JsonConvert.DeserializeObject<UpgradesResponse>(listJson);
            Assert.That(list.Catalog, Has.Count.EqualTo(1));
            Assert.That(list.Catalog[0].Component, Is.EqualTo("cannon"));
            Assert.That(list.Catalog[0].Tiers[0].Tier, Is.EqualTo(1));
            Assert.That(list.Catalog[0].Tiers[0].Costs[1].ItemKey, Is.EqualTo("ore"));
            Assert.That(list.Catalog[0].Tiers[0].Costs[1].Quantity, Is.EqualTo(20));
            Assert.That(list.Owned, Has.Count.EqualTo(2));
            Assert.That(list.Owned[0].Tier, Is.EqualTo(1));
            Assert.That(list.Owned[1].Tier, Is.EqualTo(0));

            var purchase = JsonConvert.DeserializeObject<UpgradePurchaseResponse>(purchaseJson);
            Assert.That(purchase.Upgrade.Component, Is.EqualTo("cannon"));
            Assert.That(purchase.Upgrade.Tier, Is.EqualTo(2));
            Assert.That(purchase.Spent, Has.Count.EqualTo(2));
            Assert.That(purchase.Spent[0].ItemKey, Is.EqualTo("gold"));
            Assert.That(purchase.Spent[0].Quantity, Is.EqualTo(250));
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

        [Test]
        public void TurnPlayback_StepsResolvedEventsAndCountsAppliedLossFromRemainingDeltas()
        {
            var ships = Mission10Scenario.BuildExpectedStart(Armada.Client.Bootstrap.Mission10Bootstrap.DefaultSeed).State.Ships;
            var turns = new List<Mission01TurnRecord>
            {
                new Mission01TurnRecord
                {
                    Turn = 1,
                    Events = new List<SimEvent>
                    {
                        new SimEvent { Type = "maneuver", ShipId = "player-sloop-a", Heading = 45 },
                        new SimEvent { Type = "movement", ShipId = "player-sloop-a", Position = new SimVector2 { X = 12, Y = 30 } },
                        new SimEvent
                        {
                            Type = "broadside",
                            ShipId = "player-sloop-a",
                            TargetShipId = "enemy-clipper-a",
                            Hit = true,
                            Ammo = "chain",
                            // Nominal roll is deliberately absurd: applied loss
                            // must come from the remaining delta, not damage.
                            Damage = new SimDamage { Hull = 999, Sail = 999, Crew = 999 },
                            TargetRemaining = new SimRemaining { Hp = 138, Sail = 80, Crew = 50 }
                        }
                    }
                },
                new Mission01TurnRecord
                {
                    Turn = 2,
                    Events = new List<SimEvent>
                    {
                        new SimEvent
                        {
                            Type = "ram",
                            ShipId = "player-sloop-b",
                            TargetShipId = "enemy-clipper-a",
                            HullDamage = 999,
                            SelfHullDamage = 999,
                            TargetRemaining = new SimRemaining { Hp = 120, Sail = 80, Crew = 50 },
                            RammerRemaining = new SimRemaining { Hp = 112, Sail = 80, Crew = 50 }
                        },
                        new SimEvent { Type = "unknown-future-event", ShipId = "player-sloop-a" }
                    }
                }
            };

            var playback = new TurnPlayback(ships, turns);
            var steps = new List<PlaybackStep>();
            while (playback.TryStep(out var step))
            {
                steps.Add(step);
            }

            Assert.That(steps, Has.Count.EqualTo(7));
            Assert.That(
                steps.ConvertAll(s => s.Kind),
                Is.EqualTo(new[]
                {
                    PlaybackStepKind.TurnStart,
                    PlaybackStepKind.Maneuver,
                    PlaybackStepKind.Move,
                    PlaybackStepKind.Broadside,
                    PlaybackStepKind.TurnStart,
                    PlaybackStepKind.Ram,
                    PlaybackStepKind.RunComplete
                }));
            Assert.That(playback.TryStep(out _), Is.False);

            Assert.That(steps[1].Heading, Is.EqualTo(45));
            Assert.That(steps[2].X, Is.EqualTo(12));
            Assert.That(steps[2].Y, Is.EqualTo(30));

            // Chain broadside: enemy-clipper-a starts at hp 140 / sail 110, so
            // the applied loss is 2 hull and 30 sail regardless of the roll.
            var broadside = steps[3];
            Assert.That(broadside.ChainShot, Is.True);
            Assert.That(broadside.Hit, Is.True);
            Assert.That(broadside.AppliedHull, Is.EqualTo(2));
            Assert.That(broadside.AppliedSail, Is.EqualTo(30));
            Assert.That(broadside.AppliedCrew, Is.Zero);

            // Ram: 138 -> 120 target hull, 120 -> 112 self recoil.
            var ram = steps[5];
            Assert.That(ram.Turn, Is.EqualTo(2));
            Assert.That(ram.AppliedHull, Is.EqualTo(18));
            Assert.That(ram.SelfAppliedHull, Is.EqualTo(8));

            // Totals aggregate applied loss only; ram recoil is self-inflicted
            // and never counts toward either side.
            Assert.That(playback.PlayerInflicted.Hull, Is.EqualTo(20));
            Assert.That(playback.PlayerInflicted.Sail, Is.EqualTo(30));
            Assert.That(playback.PlayerInflicted.Crew, Is.Zero);
            Assert.That(playback.EnemyInflicted.Hull, Is.Zero);

            Assert.That(PlaybackEase.Progress(-1f, 1f), Is.Zero);
            Assert.That(PlaybackEase.Progress(2f, 1f), Is.EqualTo(1f));
            Assert.That(PlaybackEase.Progress(0.5f, 1f), Is.EqualTo(0.5f).Within(0.0001f));
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
