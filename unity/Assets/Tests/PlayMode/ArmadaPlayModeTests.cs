using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class ArmadaPlayModeTests
    {
        [UnityTest]
        public IEnumerator DeterministicSeed_RepeatsAcrossFrames()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("playmode-determinism-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(9001);
                var expected = UnityEngine.Random.Range(0, int.MaxValue);

                yield return null;

                hooks.ApplySeed(9001);
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission01Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission01-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(44);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission01Flow(new FakeMission01Client(), hooks);
                var run = flow.RunAsync(44, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.TurnCount, Is.LessThanOrEqualTo(Mission01Scenario.BonusTurnTarget));

                // The flow re-applied seed 44 through DeterministicSimHooks, so the
                // next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission02Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission02-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(202);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission02Flow(new FakeMission02Client(), hooks);
                var run = flow.RunAsync(202, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.Telemetry.UpwindTurns, Is.GreaterThanOrEqualTo(Mission02Scenario.UpwindBonusTurns));

                // The flow re-applied seed 202 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private sealed class FakeMission02Client : IMission02Client
        {
            public Task<ServiceResult<Mission02StartResponse>> StartMission02Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission02StartResponse>
                {
                    Data = Mission02Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission02Outcome>> ResolveMission02Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission02Outcome>
                {
                    Data = new Mission02Outcome
                    {
                        MissionCode = Mission02Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 7,
                        TurnLimit = Mission02Scenario.TurnLimit,
                        BonusObjectives = new Mission02BonusObjectives
                        {
                            HeldWeatherGage = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission01DamageProfile
                        {
                            PlayerHullDamage = 72,
                            PlayerHullDamageFraction = 0.3,
                            PlayerRemainingHp = 168,
                            EnemyHullDamage = 240,
                            EnemyRemainingHp = 0
                        },
                        Telemetry = new Mission02Telemetry
                        {
                            RakeAttempts = 5,
                            RakeHits = 4,
                            UpwindTurns = 7,
                            UpwindByTurn = new List<bool> { true, true, true, true, true, true, true }
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission03Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission03-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(303);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission03Flow(new FakeMission03Client(), hooks);
                var run = flow.RunAsync(303, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.Telemetry.RakeHits, Is.GreaterThanOrEqualTo(Mission03Scenario.RakeHitTarget));

                // The flow re-applied seed 303 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission04Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission04-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(404);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission04Flow(new FakeMission04Client(), hooks);
                var run = flow.RunAsync(404, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.BonusObjectives.SuccessfulBoarding, Is.True);
                Assert.That(run.Result.Outcome.Telemetry.BoardingSuccesses, Is.GreaterThan(0));

                // The flow re-applied seed 404 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission05Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission05-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(505);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission05Flow(new FakeMission05Client(), hooks);
                var run = flow.RunAsync(505, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.BonusObjectives.SankFlagshipFirst, Is.True);
                Assert.That(run.Result.Outcome.Telemetry.FirstSinkTarget, Is.EqualTo(Mission05Scenario.FlagshipId));

                // The flow re-applied seed 505 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission06Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission06-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(606);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission06Flow(new FakeMission06Client(), hooks);
                var run = flow.RunAsync(606, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.Telemetry.PhaseTransitions.Count, Is.GreaterThanOrEqualTo(2));

                // The flow re-applied seed 606 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Mission07Flow_RunsMissionWithSeedAndScenarioParity()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("mission07-flow-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(707);
                var expectedDraw = UnityEngine.Random.Range(0, int.MaxValue);

                var flow = new Mission07Flow(new FakeMission07Client(), hooks);
                var run = flow.RunAsync(707, new List<List<SimOrder>>());
                while (!run.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
                Assert.That(run.Result.Outcome.Result, Is.EqualTo("win"));
                Assert.That(run.Result.Outcome.FailReason, Is.Null);
                Assert.That(run.Result.Outcome.BonusObjectives.EnemyIgnited, Is.True);
                Assert.That(run.Result.Outcome.Telemetry.IgnitionsInflicted, Is.GreaterThan(0));

                // The flow re-applied seed 707 through DeterministicSimHooks, so
                // the next draw repeats the seeded sequence.
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expectedDraw));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private sealed class FakeMission07Client : IMission07Client
        {
            public Task<ServiceResult<Mission07StartResponse>> StartMission07Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission07StartResponse>
                {
                    Data = Mission07Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission07Outcome>> ResolveMission07Async(Mission01ResolveRequest request)
            {
                // Mirrors the seed-21 gunnery outcome pinned in
                // tests/mission07.test.ts.
                return Task.FromResult(new ServiceResult<Mission07Outcome>
                {
                    Data = new Mission07Outcome
                    {
                        MissionCode = Mission07Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 9,
                        TurnLimit = Mission07Scenario.TurnLimit,
                        BonusObjectives = new Mission07BonusObjectives
                        {
                            EnemyIgnited = true,
                            Unscorched = true
                        },
                        Telemetry = new Mission07Telemetry
                        {
                            IgnitionsInflicted = 6,
                            IgnitionsSuffered = 0,
                            SlowsInflicted = 4
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator UpgradesFlow_PurchasesNextSequentialTier()
        {
            var flow = new UpgradesFlow(new FakeUpgradesClient());
            var run = flow.PurchaseNextTierAsync("11111111-1111-1111-1111-111111111111", "cannon");
            while (!run.IsCompleted)
            {
                yield return null;
            }

            Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
            Assert.That(run.Result.Purchase.Upgrade.Component, Is.EqualTo("cannon"));
            Assert.That(run.Result.Purchase.Upgrade.Tier, Is.EqualTo(2));
            Assert.That(run.Result.Purchase.Spent, Has.Count.EqualTo(2));
            Assert.That(run.Result.Purchase.Spent[0].ItemKey, Is.EqualTo("gold"));
        }

        private sealed class FakeUpgradesClient : IUpgradesClient
        {
            // Backend-shaped fixture: cannon already at tier 1, so the next
            // sequential purchase returns tier 2 with the tier-2 costs.
            public Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync()
            {
                return Task.FromResult(new ServiceResult<UpgradesResponse>
                {
                    Data = new UpgradesResponse
                    {
                        Catalog = new List<UpgradeCatalogEntry>
                        {
                            new UpgradeCatalogEntry
                            {
                                Component = "cannon",
                                Tiers = new List<UpgradeCatalogTier>
                                {
                                    new UpgradeCatalogTier
                                    {
                                        Tier = 2,
                                        Costs = new List<UpgradeCost>
                                        {
                                            new UpgradeCost { ItemKey = "gold", Quantity = 250 },
                                            new UpgradeCost { ItemKey = "ore", Quantity = 50 }
                                        }
                                    }
                                }
                            }
                        },
                        Owned = new List<OwnedUpgrade>
                        {
                            new OwnedUpgrade { Component = "cannon", Tier = 1 },
                            new OwnedUpgrade { Component = "sail", Tier = 0 },
                            new OwnedUpgrade { Component = "hull", Tier = 0 }
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<UpgradePurchaseResponse>> PurchaseAsync(UpgradePurchaseRequest request)
            {
                return Task.FromResult(new ServiceResult<UpgradePurchaseResponse>
                {
                    Data = new UpgradePurchaseResponse
                    {
                        Upgrade = new ShipUpgrade
                        {
                            PlayerId = request.PlayerId,
                            Component = request.Component,
                            Tier = request.Tier
                        },
                        Spent = new List<UpgradeCost>
                        {
                            new UpgradeCost { ItemKey = "gold", Quantity = 250 },
                            new UpgradeCost { ItemKey = "ore", Quantity = 50 }
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeMission06Client : IMission06Client
        {
            public Task<ServiceResult<Mission06StartResponse>> StartMission06Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission06StartResponse>
                {
                    Data = Mission06Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission06Outcome>> ResolveMission06Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission06Outcome>
                {
                    Data = new Mission06Outcome
                    {
                        MissionCode = Mission06Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 9,
                        TurnLimit = Mission06Scenario.TurnLimit,
                        BonusObjectives = new Mission06BonusObjectives
                        {
                            NoShipLost = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission06DamageProfile
                        {
                            PlayerHullDamage = 69,
                            PlayerHullDamageFraction = 0.19,
                            PlayerRemainingHp = 291,
                            EnemyHullDamage = 468,
                            EnemyRemainingHp = 0,
                            BossHullDamage = 468,
                            BossRemainingHp = 0
                        },
                        Telemetry = new Mission06Telemetry
                        {
                            PhaseTransitions = new List<Mission06PhaseTransition>
                            {
                                new Mission06PhaseTransition { Turn = 1, Phase = 1 },
                                new Mission06PhaseTransition { Turn = 5, Phase = 2 }
                            },
                            EnragedOnTurn = 6,
                            ReinforcementTurn = Mission06Scenario.ReinforcementTurn,
                            ReinforcementDamageDealt = 0
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeMission05Client : IMission05Client
        {
            public Task<ServiceResult<Mission05StartResponse>> StartMission05Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission05StartResponse>
                {
                    Data = Mission05Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission05Outcome>> ResolveMission05Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission05Outcome>
                {
                    Data = new Mission05Outcome
                    {
                        MissionCode = Mission05Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 8,
                        TurnLimit = Mission05Scenario.TurnLimit,
                        BonusObjectives = new Mission05BonusObjectives
                        {
                            SankFlagshipFirst = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission01DamageProfile
                        {
                            PlayerHullDamage = 110,
                            PlayerHullDamageFraction = 0.31,
                            PlayerRemainingHp = 250,
                            EnemyHullDamage = 438,
                            EnemyRemainingHp = 0
                        },
                        Telemetry = new Mission05Telemetry
                        {
                            FirstSinkTarget = Mission05Scenario.FlagshipId,
                            ChokeBlockedMoves = 2
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeMission04Client : IMission04Client
        {
            public Task<ServiceResult<Mission04StartResponse>> StartMission04Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission04StartResponse>
                {
                    Data = Mission04Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission04Outcome>> ResolveMission04Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission04Outcome>
                {
                    Data = new Mission04Outcome
                    {
                        MissionCode = Mission04Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 9,
                        TurnLimit = Mission04Scenario.TurnLimit,
                        BonusObjectives = new Mission04BonusObjectives
                        {
                            SuccessfulBoarding = true,
                            NoShipLost = true
                        },
                        DamageProfile = new Mission01DamageProfile
                        {
                            PlayerHullDamage = 60,
                            PlayerHullDamageFraction = 0.25,
                            PlayerRemainingHp = 180,
                            EnemyHullDamage = 360,
                            EnemyRemainingHp = 0
                        },
                        Telemetry = new Mission04Telemetry
                        {
                            BoardingAttempts = 10,
                            BoardingSuccesses = 8
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeMission03Client : IMission03Client
        {
            public Task<ServiceResult<Mission03StartResponse>> StartMission03Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission03StartResponse>
                {
                    Data = Mission03Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission03Outcome>> ResolveMission03Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission03Outcome>
                {
                    Data = new Mission03Outcome
                    {
                        MissionCode = Mission03Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 8,
                        TurnLimit = Mission03Scenario.TurnLimit,
                        BonusObjectives = new Mission03BonusObjectives
                        {
                            LandedRakingHits = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission03DamageProfile
                        {
                            PlayerHullDamage = 95,
                            PlayerHullDamageFraction = 0.4,
                            PlayerRemainingHp = 145,
                            EnemyHullDamage = 315,
                            EnemyRemainingHp = 0,
                            PerShip = new List<Mission03ShipDamage>()
                        },
                        Telemetry = new Mission03Telemetry
                        {
                            RakeAttempts = 7,
                            RakeHits = 4,
                            BoardingAttempts = 0,
                            BoardingSuccesses = 0
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeMission01Client : IMission01Client
        {
            public Task<ServiceResult<Mission01StartResponse>> StartMission01Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission01StartResponse>
                {
                    Data = Mission01Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission01Outcome>> ResolveMission01Async(Mission01ResolveRequest request)
            {
                return Task.FromResult(new ServiceResult<Mission01Outcome>
                {
                    Data = new Mission01Outcome
                    {
                        MissionCode = Mission01Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 4,
                        TurnLimit = Mission01Scenario.TurnLimit,
                        BonusObjectives = new Mission01BonusObjectives
                        {
                            UnderHullDamageThreshold = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission01DamageProfile
                        {
                            PlayerHullDamage = 22,
                            PlayerHullDamageFraction = 0.18,
                            PlayerRemainingHp = 98,
                            EnemyHullDamage = 108,
                            EnemyRemainingHp = 0
                        },
                        Turns = new List<Mission01TurnRecord>()
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }
    }
}
