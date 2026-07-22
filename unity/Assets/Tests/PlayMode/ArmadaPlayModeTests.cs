using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Bootstrap;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using Armada.Client.UI;
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
            public Mission01ResolveRequest LastResolveRequest;

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
                LastResolveRequest = request;
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
        public IEnumerator Mission07Flow_SendsSameOwnedTiersOnResolveAndComplete()
        {
            var missionClient = new FakeMission07Client();
            var upgradesClient = new FakeFullTierUpgradesClient();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission07Flow(missionClient, null, upgradesClient, completionClient);

            // Seed 5 loses unupgraded but wins with cannon/sail/hull all at
            // tier 3 (tests/mission07.test.ts), so the attached tiers are
            // load-bearing for the server-side win proof.
            var callerTurns = new List<List<SimOrder>>();
            var run = flow.RunAsync(5, callerTurns);
            while (!run.IsCompleted)
            {
                yield return null;
            }

            Assert.That(run.Result.Success, Is.True, run.Result.FailureReason);
            Assert.That(missionClient.LastResolveRequest.Upgrades, Is.Not.Null);
            Assert.That(missionClient.LastResolveRequest.Upgrades.Cannon, Is.EqualTo(3));
            Assert.That(missionClient.LastResolveRequest.Upgrades.Sail, Is.EqualTo(3));
            Assert.That(missionClient.LastResolveRequest.Upgrades.Hull, Is.EqualTo(3));

            var complete = flow.CompleteAsync(
                "11111111-1111-1111-1111-111111111111",
                new Dictionary<string, object> { ["outcome"] = "win" });
            while (!complete.IsCompleted)
            {
                yield return null;
            }

            Assert.That(complete.Result.Success, Is.True, complete.Result.ErrorReason);
            Assert.That(completionClient.LastCode, Is.EqualTo(Mission07Scenario.MissionCode));
            Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(5));
            // The flow snapshots the caller's turns so later mutations cannot
            // desync the completion proof from the resolved run.
            Assert.That(missionClient.LastResolveRequest.Turns, Is.Not.SameAs(callerTurns));
            Assert.That(completionClient.LastRequest.Turns, Is.SameAs(missionClient.LastResolveRequest.Turns));
            // The complete proof must carry the exact tiers the run resolved
            // with; mismatched tiers change the re-simulated outcome.
            Assert.That(completionClient.LastRequest.Upgrades, Is.SameAs(missionClient.LastResolveRequest.Upgrades));
        }

        private sealed class FakeFullTierUpgradesClient : IUpgradesClient
        {
            public Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync()
            {
                // Owned tiers mirror the fully upgraded seed-5 fixture in
                // tests/mission07.test.ts.
                return Task.FromResult(new ServiceResult<UpgradesResponse>
                {
                    Data = new UpgradesResponse
                    {
                        Owned = new List<OwnedUpgrade>
                        {
                            new OwnedUpgrade { Component = "cannon", Tier = 3 },
                            new OwnedUpgrade { Component = "sail", Tier = 3 },
                            new OwnedUpgrade { Component = "hull", Tier = 3 }
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
                    Success = false,
                    Status = HttpStatusCode.BadRequest
                });
            }
        }

        private sealed class FakeMissionCompletionClient : IMissionCompletionClient
        {
            public string LastCode;
            public MissionCompleteRequest LastRequest;

            public Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string code, MissionCompleteRequest request)
            {
                LastCode = code;
                LastRequest = request;
                return Task.FromResult(new ServiceResult<MissionCompleteResponse>
                {
                    Data = new MissionCompleteResponse(),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission07Bootstrap_DrivesRunAndCompletesWinThroughMissionUI()
        {
            var missionClient = new FakeMission07Client();
            var upgradesClient = new FakeFullTierUpgradesClient();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission07Flow(missionClient, null, upgradesClient, completionClient);

            // Inactive so MissionUIController.Start never fires a network
            // refresh; CompleteMission07 is a plain method call and does not
            // need the component to be active.
            var gameObject = new GameObject("mission07-bootstrap-test");
            gameObject.SetActive(false);
            try
            {
                var missionUI = gameObject.AddComponent<MissionUIController>();

                // Wire the plain-class [SerializeField] auth dependency the
                // same way the bootstrap composition roots do (reflection),
                // with a pre-authed state so CurrentPlayer resolves offline.
                var authService = new AuthService(null, null);
                typeof(AuthService)
                    .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(authService, new AuthState
                    {
                        Token = "test-token",
                        Player = new Player { Id = "11111111-1111-1111-1111-111111111111" }
                    });
                typeof(MissionUIController)
                    .GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(missionUI, authService);

                var drive = Mission07Bootstrap.DriveAsync(
                    flow,
                    missionUI,
                    Mission07Bootstrap.DefaultSeed,
                    Mission07Bootstrap.BuildGunneryOrders());
                while (!drive.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(drive.Result.Success, Is.True, drive.Result.FailureReason);
                Assert.That(drive.Result.Outcome.Result, Is.EqualTo("win"));

                // CompleteMission07 is async void; with fake clients it
                // finishes within a few frames.
                for (var frame = 0; completionClient.LastRequest == null && frame < 120; frame++)
                {
                    yield return null;
                }

                Assert.That(completionClient.LastCode, Is.EqualTo(Mission07Scenario.MissionCode));
                Assert.That(completionClient.LastRequest.PlayerId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
                Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(Mission07Bootstrap.DefaultSeed));
                // The completion proof must re-send the exact snapshotted
                // turns and tiers the run was resolved with.
                Assert.That(completionClient.LastRequest.Turns, Is.SameAs(missionClient.LastResolveRequest.Turns));
                Assert.That(completionClient.LastRequest.Upgrades, Is.SameAs(missionClient.LastResolveRequest.Upgrades));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private sealed class FakeMission08Client : IMission08Client
        {
            public Mission01ResolveRequest LastResolveRequest;

            public Task<ServiceResult<Mission08StartResponse>> StartMission08Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission08StartResponse>
                {
                    Data = Mission08Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission08Outcome>> ResolveMission08Async(Mission01ResolveRequest request)
            {
                LastResolveRequest = request;
                // Mirrors the seed-9 tacking outcome pinned in
                // tests/mission08.test.ts.
                return Task.FromResult(new ServiceResult<Mission08Outcome>
                {
                    Data = new Mission08Outcome
                    {
                        MissionCode = Mission08Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 9,
                        TurnLimit = Mission08Scenario.TurnLimit,
                        BonusObjectives = new Mission08BonusObjectives
                        {
                            CleanTack = false,
                            SwiftVictory = false
                        },
                        Telemetry = new Mission08Telemetry
                        {
                            ClampedManeuvers = 4,
                            UpwindManeuvers = 18,
                            DownwindManeuvers = 0
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission08Bootstrap_DrivesRunAndCompletesWinThroughMissionUI()
        {
            var missionClient = new FakeMission08Client();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission08Flow(missionClient, null, completionClient);

            // Inactive so MissionUIController.Start never fires a network
            // refresh; CompleteMission08 is a plain method call and does not
            // need the component to be active.
            var gameObject = new GameObject("mission08-bootstrap-test");
            gameObject.SetActive(false);
            try
            {
                var missionUI = gameObject.AddComponent<MissionUIController>();

                // Wire the plain-class [SerializeField] auth dependency the
                // same way the bootstrap composition roots do (reflection),
                // with a pre-authed state so CurrentPlayer resolves offline.
                var authService = new AuthService(null, null);
                typeof(AuthService)
                    .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(authService, new AuthState
                    {
                        Token = "test-token",
                        Player = new Player { Id = "11111111-1111-1111-1111-111111111111" }
                    });
                typeof(MissionUIController)
                    .GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(missionUI, authService);

                var drive = Mission08Bootstrap.DriveAsync(
                    flow,
                    missionUI,
                    Mission08Bootstrap.DefaultSeed,
                    Mission08Bootstrap.BuildTackingOrders());
                while (!drive.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(drive.Result.Success, Is.True, drive.Result.FailureReason);
                Assert.That(drive.Result.Outcome.Result, Is.EqualTo("win"));

                // CompleteMission08 is async void; with fake clients it
                // finishes within a few frames.
                for (var frame = 0; completionClient.LastRequest == null && frame < 120; frame++)
                {
                    yield return null;
                }

                Assert.That(completionClient.LastCode, Is.EqualTo(Mission08Scenario.MissionCode));
                Assert.That(completionClient.LastRequest.PlayerId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
                Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(Mission08Bootstrap.DefaultSeed));
                // The completion proof must re-send the exact snapshotted
                // turns the run was resolved with; the mission carries no
                // upgrade tiers, so the request must omit them entirely.
                Assert.That(completionClient.LastRequest.Turns, Is.SameAs(missionClient.LastResolveRequest.Turns));
                Assert.That(missionClient.LastResolveRequest.Upgrades, Is.Null);
                Assert.That(completionClient.LastRequest.Upgrades, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private sealed class FakeMission09Client : IMission09Client
        {
            public Mission01ResolveRequest LastResolveRequest;

            public Task<ServiceResult<Mission09StartResponse>> StartMission09Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission09StartResponse>
                {
                    Data = Mission09Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission09Outcome>> ResolveMission09Async(Mission01ResolveRequest request)
            {
                LastResolveRequest = request;
                // Mirrors the seed-87 double-ram outcome pinned in
                // tests/mission09.test.ts.
                return Task.FromResult(new ServiceResult<Mission09Outcome>
                {
                    Data = new Mission09Outcome
                    {
                        MissionCode = Mission09Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 7,
                        TurnLimit = Mission09Scenario.TurnLimit,
                        BonusObjectives = new Mission09BonusObjectives
                        {
                            HullBreaker = true,
                            Unrammed = true
                        },
                        Telemetry = new Mission09Telemetry
                        {
                            RamsInflicted = 2,
                            RamsSuffered = 0,
                            RamHullDamageDealt = 69,
                            RamHullDamageTaken = 46
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission09Bootstrap_DrivesRunAndCompletesWinThroughMissionUI()
        {
            var missionClient = new FakeMission09Client();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission09Flow(missionClient, null, completionClient);

            // Inactive so MissionUIController.Start never fires a network
            // refresh; CompleteMission09 is a plain method call and does not
            // need the component to be active.
            var gameObject = new GameObject("mission09-bootstrap-test");
            gameObject.SetActive(false);
            try
            {
                var missionUI = gameObject.AddComponent<MissionUIController>();

                // Wire the plain-class [SerializeField] auth dependency the
                // same way the bootstrap composition roots do (reflection),
                // with a pre-authed state so CurrentPlayer resolves offline.
                var authService = new AuthService(null, null);
                typeof(AuthService)
                    .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(authService, new AuthState
                    {
                        Token = "test-token",
                        Player = new Player { Id = "11111111-1111-1111-1111-111111111111" }
                    });
                typeof(MissionUIController)
                    .GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(missionUI, authService);

                var drive = Mission09Bootstrap.DriveAsync(
                    flow,
                    missionUI,
                    Mission09Bootstrap.DefaultSeed,
                    Mission09Bootstrap.BuildRammingOrders());
                while (!drive.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(drive.Result.Success, Is.True, drive.Result.FailureReason);
                Assert.That(drive.Result.Outcome.Result, Is.EqualTo("win"));

                // CompleteMission09 is async void; with fake clients it
                // finishes within a few frames.
                for (var frame = 0; completionClient.LastRequest == null && frame < 120; frame++)
                {
                    yield return null;
                }

                Assert.That(completionClient.LastCode, Is.EqualTo(Mission09Scenario.MissionCode));
                Assert.That(completionClient.LastRequest.PlayerId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
                Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(Mission09Bootstrap.DefaultSeed));
                // The completion proof must re-send the exact snapshotted
                // turns the run was resolved with; the mission carries no
                // upgrade tiers, so the request must omit them entirely.
                Assert.That(completionClient.LastRequest.Turns, Is.SameAs(missionClient.LastResolveRequest.Turns));
                Assert.That(missionClient.LastResolveRequest.Upgrades, Is.Null);
                Assert.That(completionClient.LastRequest.Upgrades, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private sealed class FakeMission10Client : IMission10Client
        {
            public Mission01ResolveRequest LastResolveRequest;

            public Task<ServiceResult<Mission10StartResponse>> StartMission10Async(int seed)
            {
                return Task.FromResult(new ServiceResult<Mission10StartResponse>
                {
                    Data = Mission10Scenario.BuildExpectedStart(seed),
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            public Task<ServiceResult<Mission10Outcome>> ResolveMission10Async(Mission01ResolveRequest request)
            {
                LastResolveRequest = request;
                // Mirrors the seed-2 mixed-battery outcome pinned in
                // tests/mission10.test.ts.
                return Task.FromResult(new ServiceResult<Mission10Outcome>
                {
                    Data = new Mission10Outcome
                    {
                        MissionCode = Mission10Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = "win",
                        FailReason = null,
                        TurnCount = 8,
                        TurnLimit = Mission10Scenario.TurnLimit,
                        BonusObjectives = new Mission10BonusObjectives
                        {
                            SailShredder = true,
                            MixedBattery = true
                        },
                        Telemetry = new Mission10Telemetry
                        {
                            ChainShotOrders = 6,
                            ChainShotHits = 4,
                            RoundShotHits = 7,
                            ChainSailDamageDealt = 110
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission10Bootstrap_DrivesRunAndCompletesWinThroughMissionUI()
        {
            var missionClient = new FakeMission10Client();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission10Flow(missionClient, null, completionClient);

            // Inactive so MissionUIController.Start never fires a network
            // refresh; CompleteMission10 is a plain method call and does not
            // need the component to be active.
            var gameObject = new GameObject("mission10-bootstrap-test");
            gameObject.SetActive(false);
            try
            {
                var missionUI = gameObject.AddComponent<MissionUIController>();

                // Wire the plain-class [SerializeField] auth dependency the
                // same way the bootstrap composition roots do (reflection),
                // with a pre-authed state so CurrentPlayer resolves offline.
                var authService = new AuthService(null, null);
                typeof(AuthService)
                    .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(authService, new AuthState
                    {
                        Token = "test-token",
                        Player = new Player { Id = "11111111-1111-1111-1111-111111111111" }
                    });
                typeof(MissionUIController)
                    .GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(missionUI, authService);

                var drive = Mission10Bootstrap.DriveAsync(
                    flow,
                    missionUI,
                    Mission10Bootstrap.DefaultSeed,
                    Mission10Bootstrap.BuildMixedBatteryOrders());
                while (!drive.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(drive.Result.Success, Is.True, drive.Result.FailureReason);
                Assert.That(drive.Result.Outcome.Result, Is.EqualTo("win"));

                // CompleteMission10 is async void; with fake clients it
                // finishes within a few frames.
                for (var frame = 0; completionClient.LastRequest == null && frame < 120; frame++)
                {
                    yield return null;
                }

                Assert.That(completionClient.LastCode, Is.EqualTo(Mission10Scenario.MissionCode));
                Assert.That(completionClient.LastRequest.PlayerId, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
                Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(Mission10Bootstrap.DefaultSeed));
                // The completion proof must re-send the exact snapshotted
                // turns the run was resolved with — including the per-order
                // ammo selection — and the mission carries no upgrade tiers,
                // so the request must omit them entirely.
                Assert.That(completionClient.LastRequest.Turns, Is.SameAs(missionClient.LastResolveRequest.Turns));
                Assert.That(missionClient.LastResolveRequest.Turns[0][0].Ammo, Is.EqualTo("chain"));
                Assert.That(missionClient.LastResolveRequest.Turns[3][0].Ammo, Is.Null);
                Assert.That(missionClient.LastResolveRequest.Upgrades, Is.Null);
                Assert.That(completionClient.LastRequest.Upgrades, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
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

        [UnityTest]
        public IEnumerator SpectatorRenderer_PlaysResolvedTurnsAndReportsOutcomeState()
        {
            // Inactive so Update never runs; the test drives Tick with fixed
            // deltas and asserts playback/component state, never rendered
            // output (gates run -batchmode -nographics).
            var gameObject = new GameObject("spectator-renderer-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<SpectatorRenderer>();
                var outcome = new Mission10Outcome
                {
                    MissionCode = Mission10Scenario.MissionCode,
                    Seed = Mission10Bootstrap.DefaultSeed,
                    Result = "win",
                    TurnCount = 1,
                    TurnLimit = Mission10Scenario.TurnLimit,
                    BonusObjectives = new Mission10BonusObjectives { SailShredder = true, MixedBattery = true },
                    Turns = new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "movement", ShipId = "player-sloop-a", Position = new SimVector2 { X = 40, Y = 30 } },
                                new SimEvent
                                {
                                    Type = "broadside",
                                    ShipId = "player-sloop-a",
                                    TargetShipId = "enemy-clipper-a",
                                    Hit = true,
                                    Ammo = "chain",
                                    TargetRemaining = new SimRemaining { Hp = 140, Sail = 76, Crew = 50 }
                                }
                            }
                        }
                    }
                };

                spectator.BeginOutcome(outcome);

                // Markers spawn at the pinned scenario start positions scaled
                // by the placeholder 0.1 world-units-per-sim-unit.
                Assert.That(spectator.TryGetMarkerPosition("player-sloop-a", out var start), Is.True);
                Assert.That(start.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(start.z, Is.EqualTo(3f).Within(0.001f));
                Assert.That(spectator.TryGetMarkerPosition("enemy-clipper-b", out var enemyStart), Is.True);
                Assert.That(enemyStart.x, Is.EqualTo(22f).Within(0.001f));

                var sawChainBroadside = false;
                for (var tick = 0; tick < 100 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                    if (spectator.CurrentStep?.Kind == PlaybackStepKind.Broadside)
                    {
                        Assert.That(spectator.CurrentStep.ChainShot, Is.True);
                        Assert.That(spectator.CurrentStep.AppliedSail, Is.EqualTo(34));
                        sawChainBroadside = true;
                    }
                }

                Assert.That(spectator.IsFinished, Is.True);
                Assert.That(sawChainBroadside, Is.True);

                // The movement event animated the marker to its resolved
                // position.
                Assert.That(spectator.TryGetMarkerPosition("player-sloop-a", out var moved), Is.True);
                Assert.That(moved.x, Is.EqualTo(4f).Within(0.001f));
                Assert.That(moved.z, Is.EqualTo(3f).Within(0.001f));

                // End-of-run HUD reports the outcome, bonuses, and applied
                // (remaining-delta) damage totals.
                Assert.That(spectator.HudText, Does.Contain("win"));
                Assert.That(spectator.HudText, Does.Contain("sailShredder=yes"));
                Assert.That(spectator.HudText, Does.Contain("mixedBattery=yes"));
                Assert.That(spectator.HudText, Does.Contain("sail 34"));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SpectatorRenderer_ControlsPauseStepScaleSpeedAndDriveReadoutBars()
        {
            // Inactive so Update (and its input polling) never runs; the test
            // drives Tick and the public control methods directly and asserts
            // state, never rendered output (gates run -batchmode -nographics).
            var gameObject = new GameObject("spectator-controls-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<SpectatorRenderer>();
                var outcome = new Mission10Outcome
                {
                    MissionCode = Mission10Scenario.MissionCode,
                    Seed = Mission10Bootstrap.DefaultSeed,
                    Result = "win",
                    TurnCount = 1,
                    TurnLimit = Mission10Scenario.TurnLimit,
                    BonusObjectives = new Mission10BonusObjectives(),
                    Turns = new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "movement", ShipId = "player-sloop-a", Position = new SimVector2 { X = 40, Y = 30 } },
                                new SimEvent
                                {
                                    Type = "broadside",
                                    ShipId = "player-sloop-a",
                                    TargetShipId = "enemy-clipper-a",
                                    Hit = true,
                                    Ammo = "chain",
                                    TargetRemaining = new SimRemaining { Hp = 140, Sail = 76, Crew = 50 }
                                }
                            }
                        }
                    }
                };

                spectator.BeginOutcome(outcome);

                // Bars spawn full before any event lands.
                Assert.That(spectator.TryGetReadoutFractions("enemy-clipper-a", out var hullStart, out var sailStart), Is.True);
                Assert.That(hullStart, Is.EqualTo(1f).Within(0.001f));
                Assert.That(sailStart, Is.EqualTo(1f).Within(0.001f));

                // Paused playback ignores ticks entirely.
                spectator.Pause();
                Assert.That(spectator.IsPaused, Is.True);
                Assert.That(spectator.HudText, Does.Contain("PAUSED"));
                for (var tick = 0; tick < 5; tick++)
                {
                    spectator.Tick(1f);
                }
                Assert.That(spectator.CurrentStep, Is.Null);
                Assert.That(spectator.IsFinished, Is.False);

                // StepOnce arms exactly one step: the turn banner begins,
                // completes, and playback freezes again.
                spectator.StepOnce();
                spectator.Tick(0.05f);
                Assert.That(spectator.CurrentStep?.Kind, Is.EqualTo(PlaybackStepKind.TurnStart));
                spectator.Tick(10f);
                Assert.That(spectator.CurrentStep, Is.Null);
                spectator.Tick(10f);
                Assert.That(spectator.CurrentStep, Is.Null);
                Assert.That(spectator.IsFinished, Is.False);

                // The multiplier scales elapsed time: at x4, one 0.1s tick
                // covers the whole 0.35s move step (x1 would need four).
                spectator.StepOnce();
                spectator.Tick(0.05f);
                Assert.That(spectator.CurrentStep?.Kind, Is.EqualTo(PlaybackStepKind.Move));
                spectator.SetSpeed(4f);
                Assert.That(spectator.HudText, Does.Contain("speed x4"));
                spectator.Tick(0.1f);
                Assert.That(spectator.CurrentStep, Is.Null, "x4 speed should finish the 0.35s move step in one 0.1s tick");

                // Resume at normal speed and run out the stream; the HUD
                // drops the control status once both are back to defaults.
                spectator.Resume();
                spectator.SetSpeed(1f);
                Assert.That(spectator.HudText, Does.Not.Contain("PAUSED"));
                Assert.That(spectator.HudText, Does.Not.Contain("speed x"));
                for (var tick = 0; tick < 100 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                }
                Assert.That(spectator.IsFinished, Is.True);

                // The chain broadside's remaining block (sail 110 -> 76)
                // drives the sail bar; hull is untouched.
                Assert.That(spectator.TryGetReadoutFractions("enemy-clipper-a", out var hullEnd, out var sailEnd), Is.True);
                Assert.That(hullEnd, Is.EqualTo(1f).Within(0.001f));
                Assert.That(sailEnd, Is.EqualTo(76f / 110f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield return null;
        }

        private sealed class FakePvpSimPreviewClient : ISimPreviewClient
        {
            public readonly List<SimPreviewRequest> Requests = new();

            // Scripted two-turn side-A win: turn 1 sinks bravo-a and mauls
            // bravo-b to 60 hull; turn 2 sinks bravo-b. nextState clones the
            // request state so the flow's chaining mirrors the real server
            // contract.
            public Task<SimPreviewResult> PreviewAsync(SimPreviewRequest request)
            {
                Requests.Add(request);
                var finalTurn = Requests.Count >= 2;

                int HpFor(SimShip ship) =>
                    ship.Side != "enemy" ? ship.Hp
                    : ship.Id == "bravo-frigate-a" ? 0
                    : finalTurn ? 0 : 60;

                var nextShips = new List<SimShip>();
                foreach (var ship in request.State.Ships)
                {
                    nextShips.Add(new SimShip
                    {
                        Id = ship.Id,
                        Side = ship.Side,
                        Position = new SimVector2 { X = ship.Position.X, Y = ship.Position.Y },
                        Heading = ship.Heading,
                        Speed = ship.Speed,
                        Hp = HpFor(ship),
                        Sail = ship.Sail,
                        Crew = ship.Crew
                    });
                }

                var events = finalTurn
                    ? new List<SimEvent>
                    {
                        // Far movement: sails well outside the authored
                        // opening frame, so the follow camera must re-frame.
                        new SimEvent { Type = "movement", ShipId = "alpha-frigate-a", Position = new SimVector2 { X = 400, Y = 30 } },
                        new SimEvent
                        {
                            Type = "broadside",
                            ShipId = "alpha-frigate-a",
                            TargetShipId = "bravo-frigate-b",
                            Side = "starboard",
                            Hit = true,
                            TargetRemaining = new SimRemaining { Hp = 0, Sail = 40, Crew = 50 }
                        }
                    }
                    : new List<SimEvent>
                    {
                        new SimEvent { Type = "maneuver", ShipId = "alpha-frigate-a", Heading = 15, TurnDelta = 15, SpeedDelta = 1 },
                        new SimEvent
                        {
                            Type = "broadside",
                            ShipId = "alpha-frigate-a",
                            TargetShipId = "bravo-frigate-a",
                            Side = "starboard",
                            Hit = true,
                            TargetRemaining = new SimRemaining { Hp = 0, Sail = 80, Crew = 50 }
                        },
                        new SimEvent
                        {
                            Type = "broadside",
                            ShipId = "alpha-frigate-b",
                            TargetShipId = "bravo-frigate-b",
                            Side = "starboard",
                            Hit = true,
                            Ammo = "chain",
                            TargetRemaining = new SimRemaining { Hp = 60, Sail = 40, Crew = 50 }
                        }
                    };

                return Task.FromResult(new SimPreviewResult
                {
                    Turn = request.Turn,
                    NextState = new SimState
                    {
                        Turn = request.Turn + 1,
                        Wind = request.State.Wind,
                        Ships = nextShips
                    },
                    Events = events,
                    Summary = finalTurn
                        ? new SimSummary { PlayerRemaining = 2, EnemyRemaining = 0, Sunk = new List<string> { "bravo-frigate-a", "bravo-frigate-b" } }
                        : new SimSummary { PlayerRemaining = 2, EnemyRemaining = 1, Sunk = new List<string> { "bravo-frigate-a" } },
                    Hash = finalTurn ? "pvp-fake-hash-2" : "pvp-fake-hash-1"
                });
            }
        }

        private sealed class FakeNetplayMatchClient : IPvpMatchClient
        {
            public PvpSubmitOrdersRequest LastSubmit { get; private set; }
            public string SubmittedMatchId { get; private set; }
            public int Polls { get; private set; }

            private const string MatchId = "7e57ab1e-0000-4000-8000-00000000c0de";

            private static SimState StartState() => PvpScenario.BuildInitialState();

            private static PvpMatchView View(string status, int turnNumber, SimState state, List<Mission01TurnRecord> turns, bool opponentJoined, string result = null)
            {
                return new PvpMatchView
                {
                    Id = MatchId,
                    Code = "TESTC0DE",
                    Status = status,
                    ScenarioCode = PvpScenario.ScenarioCode,
                    Seed = status == "COMPLETED" ? 11 : (int?)null,
                    TurnNumber = turnNumber,
                    TurnLimit = PvpScenario.TurnLimit,
                    Result = result,
                    State = state,
                    Turns = turns,
                    YourSide = "side_a",
                    OpponentJoined = opponentJoined
                };
            }

            private static SimState SweptState()
            {
                var state = StartState();
                state.Turn = 2;
                foreach (var ship in state.Ships)
                {
                    if (ship.Side == "enemy")
                    {
                        ship.Hp = 0;
                    }
                }

                return state;
            }

            private static List<Mission01TurnRecord> ResolvedTurns()
            {
                return new List<Mission01TurnRecord>
                {
                    new Mission01TurnRecord
                    {
                        Turn = 1,
                        Hash = "net-fake-hash",
                        Summary = new SimSummary { PlayerRemaining = 2, EnemyRemaining = 0, Sunk = new List<string> { "bravo-frigate-a", "bravo-frigate-b" } },
                        Events = new List<SimEvent>
                        {
                            new SimEvent
                            {
                                Type = "broadside",
                                ShipId = "alpha-frigate-a",
                                TargetShipId = "bravo-frigate-a",
                                Side = "starboard",
                                Hit = true,
                                Ammo = "chain",
                                TargetRemaining = new SimRemaining { Hp = 0, Sail = 40, Crew = 50 }
                            },
                            new SimEvent
                            {
                                Type = "broadside",
                                ShipId = "alpha-frigate-b",
                                TargetShipId = "bravo-frigate-b",
                                Side = "starboard",
                                Hit = true,
                                TargetRemaining = new SimRemaining { Hp = 0, Sail = 80, Crew = 50 }
                            }
                        }
                    }
                };
            }

            public Task<ServiceResult<PvpMatchResponse>> CreateMatchAsync()
            {
                return Ok(View("WAITING_FOR_OPPONENT", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: false));
            }

            public Task<ServiceResult<PvpMatchResponse>> JoinMatchAsync(string code)
            {
                throw new System.InvalidOperationException("creator flow never joins");
            }

            // Simulates a transport drop on the next submission; the server
            // never received it, so the reconcile poll shows no staged
            // orders.
            public bool FailNextSubmit;
            private bool _reconcilePending;

            public Task<ServiceResult<PvpSubmitOrdersResponse>> SubmitOrdersAsync(string matchId, PvpSubmitOrdersRequest request)
            {
                if (FailNextSubmit)
                {
                    FailNextSubmit = false;
                    _reconcilePending = true;
                    return Task.FromResult(new ServiceResult<PvpSubmitOrdersResponse>
                    {
                        Success = false,
                        Status = 0,
                        ErrorReason = "transport_dropped"
                    });
                }

                SubmittedMatchId = matchId;
                LastSubmit = request;
                // The opponent has not submitted yet: orders staged only.
                return Task.FromResult(new ServiceResult<PvpSubmitOrdersResponse>
                {
                    Data = new PvpSubmitOrdersResponse
                    {
                        Resolved = false,
                        Match = View("IN_PROGRESS", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: true)
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }

            // When set, the next poll reports the match as expired (the
            // server's abandonment TTL fired).
            public bool ExpireOnNextPoll;

            public Task<ServiceResult<PvpMatchResponse>> GetMatchAsync(string matchId)
            {
                if (ExpireOnNextPoll)
                {
                    ExpireOnNextPoll = false;
                    return Ok(View("EXPIRED", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: false));
                }

                if (_reconcilePending)
                {
                    // The failed submission never landed: live match, no
                    // staged orders from this side (YouSubmitted false).
                    _reconcilePending = false;
                    return Ok(View("IN_PROGRESS", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: true));
                }

                Polls++;
                if (Polls == 1)
                {
                    // Still waiting for the opponent to join.
                    return Ok(View("WAITING_FOR_OPPONENT", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: false));
                }
                if (Polls == 2)
                {
                    // Opponent joined; the match is live.
                    return Ok(View("IN_PROGRESS", 1, StartState(), new List<Mission01TurnRecord>(), opponentJoined: true));
                }

                // After our submission the opponent's orders landed and the
                // server resolved turn 1 as a side A sweep.
                return Ok(View("COMPLETED", 2, SweptState(), ResolvedTurns(), opponentJoined: true, result: "side_a"));
            }

            private static Task<ServiceResult<PvpMatchResponse>> Ok(PvpMatchView view)
            {
                return Task.FromResult(new ServiceResult<PvpMatchResponse>
                {
                    Data = new PvpMatchResponse { Match = view },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator PvpNetplay_CreatorFlowSubmitsOwnSideOnlyAndPlaysServerResolvedTurn()
        {
            // Inactive objects so Update never runs; the test drives
            // Advance/Tick with fixed deltas and asserts state, never
            // rendered output.
            var spectatorObject = new GameObject("pvp-netplay-spectator-test");
            spectatorObject.SetActive(false);
            var controllerObject = new GameObject("pvp-netplay-controller-test");
            controllerObject.SetActive(false);
            try
            {
                var spectator = spectatorObject.AddComponent<SpectatorRenderer>();
                var controller = controllerObject.AddComponent<PvpNetplayUIController>();
                var fakeClient = new FakeNetplayMatchClient();
                var flow = new PvpNetplayFlow(fakeClient);

                controller.Compose(flow, spectator);
                controller.ShowMenu();
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.Menu));

                // Create: the fake resolves synchronously here, but awaits of
                // completed tasks are a platform timing detail — wait bounded
                // for each phase instead of asserting intermediate states.
                controller.OnCreateMatch();
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.WaitingForOpponentJoin
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.WaitingForOpponentJoin));

                // First poll: still waiting. Second poll: opponent joined,
                // order entry opens for OUR side only.
                controller.Advance(2.5f);
                deadline.Restart();
                while (fakeClient.Polls < 1 && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.WaitingForOpponentJoin));

                controller.Advance(2.5f);
                deadline.Restart();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.OrderEntry
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.OrderEntry));
                Assert.That(controller.CurrentSession.SideLabel, Is.EqualTo("A"));
                Assert.That(controller.CurrentSession.Drafts, Has.Count.EqualTo(2));

                // An ambiguous submit failure (transport drop) must NOT be
                // terminal: the reconcile poll finds no staged orders on the
                // server and reopens order entry for re-authoring.
                fakeClient.FailNextSubmit = true;
                controller.OnCycleTarget();
                controller.OnConfirmOrders();
                deadline.Restart();
                while (controller.Phase == PvpNetplayUIController.NetplayPhase.OrderEntry
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.WaitingForResolution));
                controller.Advance(0.1f);
                deadline.Restart();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.OrderEntry
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.OrderEntry));
                Assert.That(fakeClient.LastSubmit, Is.Null, "the dropped submission never reached the server");

                // Author side A's orders: alpha-a chain-shots bravo-a.
                controller.OnCycleTarget();
                controller.OnToggleAmmo();
                controller.OnNextShip();
                controller.OnCycleTarget();
                controller.OnCycleTarget();
                controller.OnConfirmOrders();

                deadline.Restart();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.WaitingForResolution
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.WaitingForResolution));

                // The submission carried ONLY our side's ships, bound to the
                // server's current turn.
                Assert.That(fakeClient.SubmittedMatchId, Is.EqualTo(flow.MatchId));
                Assert.That(fakeClient.LastSubmit.TurnNumber, Is.EqualTo(1));
                Assert.That(fakeClient.LastSubmit.Orders, Has.Count.EqualTo(2));
                foreach (var order in fakeClient.LastSubmit.Orders)
                {
                    Assert.That(order.ShipId, Does.StartWith("alpha-"));
                }
                Assert.That(fakeClient.LastSubmit.Orders[0].TargetShipId, Is.EqualTo("bravo-frigate-a"));
                Assert.That(fakeClient.LastSubmit.Orders[0].Ammo, Is.EqualTo("chain"));
                Assert.That(fakeClient.LastSubmit.Orders[1].TargetShipId, Is.EqualTo("bravo-frigate-b"));
                Assert.That(fakeClient.LastSubmit.Orders[1].Ammo, Is.Null);

                // The resolution poll discovers the server-resolved turn and
                // hands it to the spectator from the pre-turn snapshot.
                controller.Advance(2.5f);
                deadline.Restart();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.Playback
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.Playback));

                Assert.That(spectator.TryGetMarkerPosition("bravo-frigate-b", out var bravoStart), Is.True);
                Assert.That(bravoStart.x, Is.EqualTo(22f).Within(0.001f));

                var sawChainBroadside = false;
                for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                    if (spectator.CurrentStep?.Kind == PlaybackStepKind.Broadside && spectator.CurrentStep.ChainShot)
                    {
                        sawChainBroadside = true;
                    }
                }
                Assert.That(spectator.IsFinished, Is.True);
                Assert.That(sawChainBroadside, Is.True);
                Assert.That(spectator.HudText, Does.Contain("VICTORY"));
                Assert.That(spectator.HudText, Does.Contain("side A applied: hull 240"));

                // Playback completion lands on the match verdict.
                controller.Advance(0.1f);
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.Finished));
            }
            finally
            {
                UnityEngine.Object.Destroy(spectatorObject);
                UnityEngine.Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PvpNetplay_ExpiredMatchEndsTheSessionInsteadOfPollingForever()
        {
            var controllerObject = new GameObject("pvp-netplay-expiry-test");
            controllerObject.SetActive(false);
            try
            {
                var controller = controllerObject.AddComponent<PvpNetplayUIController>();
                var fakeClient = new FakeNetplayMatchClient();
                var flow = new PvpNetplayFlow(fakeClient);
                controller.Compose(flow, null);
                controller.ShowMenu();

                controller.OnCreateMatch();
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.WaitingForOpponentJoin
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.WaitingForOpponentJoin));

                // The server-side abandonment TTL fires; the next poll must
                // finish the session rather than keep waiting on the match.
                fakeClient.ExpireOnNextPoll = true;
                controller.Advance(2.5f);
                deadline.Restart();
                while (controller.Phase != PvpNetplayUIController.NetplayPhase.Finished
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpNetplayUIController.NetplayPhase.Finished));
            }
            finally
            {
                UnityEngine.Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PvpHotseat_BothSidesOrdersResolveOneTurnAndSpectatorPlaysItBack()
        {
            // Inactive objects so Update never runs; the test drives the
            // controller's public handlers and the spectator's Tick directly
            // and asserts state, never rendered output.
            var spectatorObject = new GameObject("pvp-spectator-test");
            spectatorObject.SetActive(false);
            var controllerObject = new GameObject("pvp-controller-test");
            controllerObject.SetActive(false);
            var followCameraObject = new GameObject("pvp-follow-camera-test");
            followCameraObject.SetActive(false);
            try
            {
                var spectator = spectatorObject.AddComponent<SpectatorRenderer>();
                var controller = controllerObject.AddComponent<PvpHotseatUIController>();

                // Follow camera wired the way the PvP scene builders do it;
                // a pinned aspect keeps the re-framing math deterministic.
                var followCamera = followCameraObject.AddComponent<Camera>();
                followCamera.orthographic = true;
                followCamera.orthographicSize = 8.5f;
                followCamera.aspect = 16f / 9f;
                followCamera.transform.position = new Vector3(11f, 20f, 0f);
                typeof(SpectatorRenderer)
                    .GetField("followCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(spectator, followCamera);
                var fakeClient = new FakePvpSimPreviewClient();
                var flow = new PvpHotseatFlow(fakeClient);

                controller.Compose(flow, spectator);
                controller.BeginMatch();

                // Turn 1, side A: alpha-a broadsides bravo-a, alpha-b holds.
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.SideAEntry));
                Assert.That(controller.CurrentSession.SideLabel, Is.EqualTo("A"));
                controller.OnCycleTarget();
                controller.OnTurnRight();
                controller.OnSpeedUp();
                controller.OnConfirmSide();

                // Confirm lands on the hand-the-seat interstitial, so a
                // double-press can never submit default side-B orders; the
                // next confirm opens a fresh side-B session.
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.SideBHandoff));
                Assert.That(controller.CurrentSession, Is.Null);
                controller.OnConfirmSide();

                // Turn 1, side B: bravo-a fires chain at alpha-a, bravo-b
                // maneuvers.
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.SideBEntry));
                Assert.That(controller.CurrentSession.SideLabel, Is.EqualTo("B"));
                controller.OnCycleTarget();
                controller.OnToggleAmmo();
                controller.OnNextShip();
                controller.OnTurnLeft();
                controller.OnConfirmSide();

                // The fake resolves synchronously on this platform, but a
                // pending-task assertion here would be timing-dependent
                // (completed-task awaits continue synchronously); wait
                // bounded for the playback phase instead.
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (controller.Phase != PvpHotseatUIController.HotseatPhase.Playback
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.Playback));

                // The submitted request carried BOTH sides' orders for one
                // turn of the pinned scenario, with the pinned modifier set.
                Assert.That(fakeClient.Requests, Has.Count.EqualTo(1));
                var request = fakeClient.Requests[0];
                Assert.That(request.Seed, Is.EqualTo(PvpScenario.DefaultSeed));
                Assert.That(request.Turn, Is.EqualTo(1));
                Assert.That(request.Modifiers.ChainShot, Is.True);
                Assert.That(request.Modifiers.WindMovement, Is.True);
                Assert.That(request.Modifiers.Ramming, Is.True);
                Assert.That(request.Modifiers.ShipUpgrades, Is.Null);
                Assert.That(request.State.Ships, Has.Count.EqualTo(4));
                Assert.That(request.Orders, Has.Count.EqualTo(4));

                var orderByShip = new Dictionary<string, SimOrder>();
                foreach (var order in request.Orders)
                {
                    orderByShip[order.ShipId] = order;
                }

                Assert.That(orderByShip["alpha-frigate-a"].Action, Is.EqualTo("broadside"));
                Assert.That(orderByShip["alpha-frigate-a"].TargetShipId, Is.EqualTo("bravo-frigate-a"));
                Assert.That(orderByShip["alpha-frigate-a"].TurnDelta, Is.EqualTo(15));
                Assert.That(orderByShip["alpha-frigate-a"].SpeedDelta, Is.EqualTo(1));
                Assert.That(orderByShip["alpha-frigate-b"].Action, Is.EqualTo("maneuver"));
                Assert.That(orderByShip["bravo-frigate-a"].Action, Is.EqualTo("broadside"));
                Assert.That(orderByShip["bravo-frigate-a"].TargetShipId, Is.EqualTo("alpha-frigate-a"));
                Assert.That(orderByShip["bravo-frigate-a"].Ammo, Is.EqualTo("chain"));
                Assert.That(orderByShip["bravo-frigate-b"].TurnDelta, Is.EqualTo(-15));

                // Spectator playback animates the resolved turn from the
                // turn-start ship snapshot; markers spawn at the pinned
                // scenario positions (0.1 world units per sim unit).
                Assert.That(spectator.TryGetMarkerPosition("alpha-frigate-a", out var alphaStart), Is.True);
                Assert.That(alphaStart.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(alphaStart.z, Is.EqualTo(3f).Within(0.001f));
                Assert.That(spectator.TryGetMarkerPosition("bravo-frigate-b", out var bravoStart), Is.True);
                Assert.That(bravoStart.x, Is.EqualTo(22f).Within(0.001f));

                var sawChainBroadside = false;
                for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                    if (spectator.CurrentStep?.Kind == PlaybackStepKind.Broadside && spectator.CurrentStep.ChainShot)
                    {
                        sawChainBroadside = true;
                    }
                }
                Assert.That(spectator.IsFinished, Is.True);
                Assert.That(sawChainBroadside, Is.True);
                Assert.That(spectator.HudText, Does.Contain("Turn 1 complete"));

                // An ongoing match loops back to side A entry with the
                // chained server state: bravo-a is sunk, bravo-b still up.
                controller.PollPlayback();
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.SideAEntry));
                Assert.That(flow.MatchResult, Is.EqualTo(PvpHotseatFlow.ResultOngoing));
                Assert.That(flow.TurnNumber, Is.EqualTo(2));

                // Turn 2, side A: the only living target is bravo-b.
                controller.OnCycleTarget();
                Assert.That(controller.CurrentSession.CurrentDraft.TargetShipId, Is.EqualTo("bravo-frigate-b"));
                controller.OnConfirmSide();
                controller.OnConfirmSide();

                // Turn 2, side B: only bravo-b is left to command.
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.SideBEntry));
                Assert.That(controller.CurrentSession.Drafts, Has.Count.EqualTo(1));
                controller.OnConfirmSide();

                deadline.Restart();
                while (controller.Phase != PvpHotseatUIController.HotseatPhase.Playback
                    && deadline.Elapsed.TotalSeconds < 5)
                {
                    yield return null;
                }
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.Playback));

                // The turn-2 request chained the resolved turn-1 state.
                Assert.That(fakeClient.Requests, Has.Count.EqualTo(2));
                var second = fakeClient.Requests[1];
                Assert.That(second.Turn, Is.EqualTo(2));
                Assert.That(second.Orders, Has.Count.EqualTo(3));
                foreach (var ship in second.State.Ships)
                {
                    if (ship.Id == "bravo-frigate-a")
                    {
                        Assert.That(ship.Hp, Is.Zero);
                    }
                    if (ship.Id == "bravo-frigate-b")
                    {
                        Assert.That(ship.Hp, Is.EqualTo(60));
                    }
                }

                // Mid-battle playback keeps readout bars on the battle-start
                // maxima: bravo-b at 60/120 hull reads half, not full.
                Assert.That(spectator.TryGetReadoutFractions("bravo-frigate-b", out var hullMid, out _), Is.True);
                Assert.That(hullMid, Is.EqualTo(0.5f).Within(0.001f));

                for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                }
                Assert.That(spectator.IsFinished, Is.True);

                // The far movement (sim 400 → world x 40) sailed outside the
                // authored frame; the follow camera re-centered on the fleet
                // spread (world x 0..40 → 20) and zoomed out to fit it:
                // max(8.5 min, halfZ, halfX/aspect) = (40/2 + 2) / (16/9).
                Assert.That(followCamera.transform.position.x, Is.EqualTo(20f).Within(0.5f));
                Assert.That(followCamera.orthographicSize, Is.EqualTo(22f / (16f / 9f)).Within(0.01f));

                // Generic completion line: the match verdict plus per-side
                // applied (remaining-delta) loss totals for the final turn
                // (bravo-b's last 60 hull).
                Assert.That(spectator.HudText, Does.Contain("SIDE A WINS at turn 2"));
                Assert.That(spectator.HudText, Does.Contain("side A applied: hull 60"));
                Assert.That(spectator.HudText, Does.Contain("side B applied: hull 0"));

                // Playback completion advances the hot-seat loop to the
                // match verdict.
                controller.PollPlayback();
                Assert.That(controller.Phase, Is.EqualTo(PvpHotseatUIController.HotseatPhase.Finished));
                Assert.That(flow.MatchResult, Is.EqualTo(PvpHotseatFlow.ResultSideA));
                Assert.That(flow.TurnNumber, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.Destroy(spectatorObject);
                UnityEngine.Object.Destroy(controllerObject);
                UnityEngine.Object.Destroy(followCameraObject);
            }

            yield return null;
        }
    }
}
