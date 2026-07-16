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
