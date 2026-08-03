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
                        TurnCount = 10,
                        TurnLimit = Mission06Scenario.TurnLimit,
                        BonusObjectives = new Mission06BonusObjectives
                        {
                            NoShipLost = true,
                            WithinTurnTarget = true
                        },
                        DamageProfile = new Mission06DamageProfile
                        {
                            PlayerHullDamage = 109,
                            PlayerHullDamageFraction = 0.3,
                            PlayerRemainingHp = 251,
                            EnemyHullDamage = 576,
                            EnemyRemainingHp = 0,
                            BossHullDamage = 468,
                            BossRemainingHp = 0
                        },
                        Telemetry = new Mission06Telemetry
                        {
                            PhaseTransitions = new List<Mission06PhaseTransition>
                            {
                                new Mission06PhaseTransition { Turn = 1, Phase = 1 },
                                new Mission06PhaseTransition { Turn = 4, Phase = 2 }
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
                            EnemyHullDamage = 345,
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
        public IEnumerator FollowCamera_NeverLetsALivingShipOutrunTheFrame()
        {
            // Containment guarantee under easing (Codex P1 on #90): during a
            // long outward move, every tick's frame must still contain the
            // moving ship plus padding, even while the center lags behind.
            var spectatorObject = new GameObject("follow-containment-test");
            spectatorObject.SetActive(false);
            var cameraObject = new GameObject("follow-containment-camera");
            cameraObject.SetActive(false);
            try
            {
                var spectator = spectatorObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                var followCamera = cameraObject.AddComponent<Camera>();
                followCamera.orthographic = true;
                followCamera.orthographicSize = 8.5f;
                followCamera.aspect = 16f / 9f;
                followCamera.transform.position = new Vector3(11f, 20f, 0f);
                typeof(Armada.Client.Playback.SpectatorRenderer)
                    .GetField("followCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(spectator, followCamera);

                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "anchor", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "runner", Side = "enemy", Position = new SimVector2 { X = 20, Y = 0 }, Heading = 0, Speed = 10, Hp = 140, Sail = 110, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "movement", ShipId = "runner", Position = new SimVector2 { X = 400, Y = 0 } }
                            }
                        }
                    },
                    turnLimit: 1,
                    introLine: "containment test",
                    completionLine: "done");

                for (var tick = 0; tick < 80; tick++)
                {
                    spectator.Tick(0.1f);
                    if (!spectator.TryGetMarkerPosition("runner", out var runner))
                    {
                        continue;
                    }

                    var halfWidth = followCamera.orthographicSize * followCamera.aspect;
                    var left = followCamera.transform.position.x - halfWidth;
                    var right = followCamera.transform.position.x + halfWidth;
                    Assert.That(runner.x, Is.GreaterThanOrEqualTo(left).And.LessThanOrEqualTo(right),
                        $"runner escaped the frame at tick {tick}");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(spectatorObject);
                UnityEngine.Object.Destroy(cameraObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator OrderPanelView_RendersOneStructuredRowPerDraftWithActiveCursor()
        {
            // W4 HUD IA: the panel replaces the Describe() blob with one row
            // per ship draft; the cursor and emphasis follow the active ship
            // and Clear hides every row.
            var gameObject = new GameObject("order-panel-test", typeof(RectTransform));
            gameObject.SetActive(false);
            try
            {
                var panel = gameObject.AddComponent<Armada.Client.UI.OrderPanelView>();
                var session = new Armada.Client.Services.PvpOrderSession(
                    "A",
                    new List<SimShip>
                    {
                        new SimShip { Id = "alpha-frigate-a", Side = "player", Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "alpha-frigate-b", Side = "player", Hp = 120, Sail = 80, Crew = 50 }
                    },
                    new List<SimShip>
                    {
                        new SimShip { Id = "bravo-frigate-a", Side = "enemy", Hp = 120, Sail = 80, Crew = 50 }
                    });
                session.CycleTarget();
                session.AdjustTurn(1);

                panel.Render(session);
                Assert.That(panel.VisibleRowCount, Is.EqualTo(2));
                Assert.That(panel.RowCaption(0), Does.Contain("▶").And.Contain("alpha-frigate-a"));
                Assert.That(panel.RowCaption(0), Does.Contain("+15"));
                Assert.That(panel.RowCaption(0), Does.Contain("fire round at bravo-frigate-a"));
                Assert.That(panel.RowCaption(1), Does.Not.Contain("▶"));
                Assert.That(panel.RowCaption(1), Does.Contain("hold fire"));

                session.NextShip();
                panel.Render(session);
                Assert.That(panel.RowCaption(1), Does.Contain("▶"));

                panel.Clear();
                Assert.That(panel.VisibleRowCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator SpectatorRenderer_ConditionsReadoutTracksWindAndTurnNumerics()
        {
            // W4 conditions zone (art-direction.md §7 item 3): the renderer
            // composes a compact numeric wind/turn line from the wind it is
            // given and the TurnStart steps it plays; ShowBoard (authoring
            // states, no queued playback) drops back to wind-only.
            var gameObject = new GameObject("conditions-readout-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                var ships = new List<SimShip>
                {
                    new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                    new SimShip { Id = "enemy-clipper-a", Side = "enemy", Position = new SimVector2 { X = 20, Y = 0 }, Heading = 180, Speed = 3, Hp = 140, Sail = 110, Crew = 50 }
                };

                spectator.BeginTurns(
                    ships,
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "movement", ShipId = "player-sloop-a", Position = new SimVector2 { X = 4, Y = 0 } }
                            }
                        }
                    },
                    turnLimit: 40,
                    introLine: "conditions test",
                    completionLine: "done",
                    wind: new SimWind { Direction = 90, Speed = 5 });

                // Before any TurnStart the readout is wind-only.
                Assert.That(spectator.ConditionsText, Is.EqualTo("Wind 90°/5"));

                var sawTurnReadout = false;
                for (var tick = 0; tick < 100 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                    if (spectator.ConditionsText == "Wind 90°/5 | Turn 1/40")
                    {
                        sawTurnReadout = true;
                    }
                }

                Assert.That(spectator.IsFinished, Is.True);
                Assert.That(sawTurnReadout, Is.True);

                // Authoring snapshots queue no playback, so no turn number is
                // authoritative: back to wind-only.
                spectator.ShowBoard(ships, "authoring", wind: new SimWind { Direction = 270, Speed = 2 });
                Assert.That(spectator.ConditionsText, Is.EqualTo("Wind 270°/2"));

                // Pure composition covers the remaining shapes.
                Assert.That(Armada.Client.Playback.SpectatorRenderer.ComposeConditions(null, 3, 40), Is.EqualTo("Turn 3/40"));
                Assert.That(Armada.Client.Playback.SpectatorRenderer.ComposeConditions(null, 3, 0), Is.EqualTo("Turn 3"));
                Assert.That(Armada.Client.Playback.SpectatorRenderer.ComposeConditions(null, 0, 40), Is.EqualTo(string.Empty));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BottomStripStacker_PortraitShrinksCellsCentersRowsAndStacksRisers()
        {
            // W4 portrait restructure: on a portrait-aspect HUD area the
            // stacker swaps strip grids to the smaller portrait cells (three
            // columns instead of two) and re-stacks the order text/rows
            // risers above the top strip; a landscape pass restores the
            // authored cells and alignment. Drives Restack directly, exactly
            // like the headless capture harness — no visual assertions.
            var containerObject = new GameObject("stacker-container", typeof(RectTransform));
            var stackerObject = new GameObject("stacker-test");
            try
            {
                var container = (RectTransform)containerObject.transform;
                // Portrait 9:16 in reference units (1080-height scaling).
                container.sizeDelta = new Vector2(608f, 1080f);

                var orderStrip = CreateStackerStrip(container, cellCount: 9);
                var playbackStrip = CreateStackerStrip(container, cellCount: 4);
                var orderLabel = CreateStackerRiser(container, height: 200f);
                var orderRows = CreateStackerRiser(container, height: 200f);

                var stacker = stackerObject.AddComponent<Armada.Client.UI.BottomStripStacker>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Armada.Client.UI.BottomStripStacker).GetField("strips", flags)
                    .SetValue(stacker, new[] { orderStrip, playbackStrip });
                typeof(Armada.Client.UI.BottomStripStacker).GetField("risers", flags)
                    .SetValue(stacker, new[] { orderLabel, orderRows });

                stacker.Restack();

                var orderGrid = orderStrip.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                var playbackGrid = playbackStrip.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Assert.That(orderGrid.cellSize, Is.EqualTo(new Vector2(150f, 110f)));
                Assert.That(orderGrid.childAlignment, Is.EqualTo(TextAnchor.LowerCenter));
                // 560-unit strip width fits three 150-unit columns: nine
                // cells wrap to three rows, four cells to two.
                Assert.That(orderStrip.rect.height, Is.EqualTo(354f).Within(0.001f));
                Assert.That(playbackStrip.rect.height, Is.EqualTo(232f).Within(0.001f));
                Assert.That(orderStrip.anchoredPosition.y, Is.EqualTo(24f).Within(0.001f));
                Assert.That(playbackStrip.anchoredPosition.y, Is.EqualTo(390f).Within(0.001f));
                // Risers stack above the top strip, bottom-first — and
                // compact to the portrait height (Codex P2 on PR #101: the
                // authored 200-unit rects on top of three wrapped strips
                // overflowed the 1080 canvas in the netplay scene), so the
                // whole stack stays inside the viewport.
                Assert.That(orderLabel.rect.height, Is.EqualTo(140f).Within(0.001f));
                Assert.That(orderRows.rect.height, Is.EqualTo(140f).Within(0.001f));
                Assert.That(orderLabel.anchoredPosition.y, Is.EqualTo(634f).Within(0.001f));
                Assert.That(orderRows.anchoredPosition.y, Is.EqualTo(786f).Within(0.001f));
                Assert.That(orderRows.anchoredPosition.y + orderRows.rect.height, Is.LessThan(1080f));

                // Landscape restores the authored cells, alignment, and
                // single-row strip heights.
                container.sizeDelta = new Vector2(1920f, 1080f);
                stacker.Restack();
                Assert.That(orderGrid.cellSize, Is.EqualTo(new Vector2(190f, 140f)));
                Assert.That(orderGrid.childAlignment, Is.EqualTo(TextAnchor.LowerLeft));
                Assert.That(playbackGrid.cellSize, Is.EqualTo(new Vector2(190f, 140f)));
                Assert.That(orderStrip.rect.height, Is.EqualTo(140f).Within(0.001f));
                Assert.That(playbackStrip.anchoredPosition.y, Is.EqualTo(176f).Within(0.001f));
                // Authored riser heights come back with the landscape pass.
                Assert.That(orderLabel.rect.height, Is.EqualTo(200f).Within(0.001f));
                Assert.That(orderLabel.anchoredPosition.y, Is.EqualTo(328f).Within(0.001f));
                Assert.That(orderRows.anchoredPosition.y, Is.EqualTo(540f).Within(0.001f));

                // Pure helpers.
                Assert.That(Armada.Client.UI.BottomStripStacker.IsPortrait(608f, 1080f), Is.True);
                Assert.That(Armada.Client.UI.BottomStripStacker.IsPortrait(1920f, 1080f), Is.False);
                Assert.That(Armada.Client.UI.BottomStripStacker.CenteredInRow(TextAnchor.LowerLeft), Is.EqualTo(TextAnchor.LowerCenter));
                Assert.That(Armada.Client.UI.BottomStripStacker.CenteredInRow(TextAnchor.UpperRight), Is.EqualTo(TextAnchor.UpperCenter));
            }
            finally
            {
                UnityEngine.Object.Destroy(containerObject);
                UnityEngine.Object.Destroy(stackerObject);
            }

            yield break;
        }

        // Mirrors the scene builders' CreateButtonGrid geometry: full-width
        // bottom-anchored strip, 190×140 authored cells, 12 spacing, wrap via
        // GridLayoutGroup + ContentSizeFitter.
        private static RectTransform CreateStackerStrip(Transform parent, int cellCount)
        {
            var gridObject = new GameObject(
                "strip",
                typeof(RectTransform),
                typeof(UnityEngine.UI.GridLayoutGroup),
                typeof(UnityEngine.UI.ContentSizeFitter));
            gridObject.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)gridObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-48f, 0f);
            var grid = gridObject.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(190f, 140f);
            grid.spacing = new Vector2(12f, 12f);
            grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.LowerLeft;
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.LowerLeft;
            gridObject.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            for (var i = 0; i < cellCount; i++)
            {
                var cell = new GameObject($"cell-{i}", typeof(RectTransform));
                cell.transform.SetParent(gridObject.transform, worldPositionStays: false);
            }

            return rect;
        }

        // Mirrors the builders' bottom-anchored order text/rows rects.
        private static RectTransform CreateStackerRiser(Transform parent, float height)
        {
            var riserObject = new GameObject("riser", typeof(RectTransform));
            riserObject.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)riserObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-48f, height);
            return rect;
        }

        [UnityTest]
        public IEnumerator FollowCamera_TightensOnLivingShipsAndIgnoresWrecks()
        {
            // W3 composition contract: the camera converges toward the living
            // ships' framing, may tighten to the followMinSize floor (5, not
            // the authored 8.5 opening), and a distant wreck no longer pins
            // the frame open.
            var spectatorObject = new GameObject("follow-camera-w3-test");
            spectatorObject.SetActive(false);
            var cameraObject = new GameObject("follow-camera-w3-camera");
            cameraObject.SetActive(false);
            try
            {
                var spectator = spectatorObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                var followCamera = cameraObject.AddComponent<Camera>();
                followCamera.orthographic = true;
                followCamera.orthographicSize = 8.5f;
                followCamera.aspect = 16f / 9f;
                followCamera.transform.position = new Vector3(11f, 20f, 0f);
                typeof(Armada.Client.Playback.SpectatorRenderer)
                    .GetField("followCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(spectator, followCamera);

                // Two living ships two sim units apart mid-board, plus a
                // wreck (hp 0 at spawn) far east that must not affect
                // framing.
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "alive-a", Side = "player", Position = new SimVector2 { X = 100, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "alive-b", Side = "enemy", Position = new SimVector2 { X = 120, Y = 0 }, Heading = 180, Speed = 3, Hp = 140, Sail = 110, Crew = 50 },
                        new SimShip { Id = "wreck", Side = "enemy", Position = new SimVector2 { X = 400, Y = 0 }, Heading = 180, Speed = 0, Hp = 0, Sail = 0, Crew = 0 }
                    },
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord { Turn = 1, Events = new List<SimEvent>() }
                    },
                    turnLimit: 1,
                    introLine: "camera test",
                    completionLine: "done");

                // Enough fixed ticks for the exponential ease to converge.
                for (var tick = 0; tick < 60 && !spectator.IsFinished; tick++)
                {
                    spectator.Tick(0.5f);
                }
                for (var tick = 0; tick < 60; tick++)
                {
                    spectator.Tick(0.5f);
                }

                // Living pair spans world x 10..12: center 11, tiny extents →
                // the floor (5) wins, far below the old 8.5 invariant; the
                // wreck at world x 40 is ignored.
                Assert.That(followCamera.orthographicSize, Is.EqualTo(5f).Within(0.05f));
                Assert.That(followCamera.transform.position.x, Is.EqualTo(11f).Within(0.1f));
            }
            finally
            {
                UnityEngine.Object.Destroy(spectatorObject);
                UnityEngine.Object.Destroy(cameraObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator SpectatorRenderer_SinksShipsRendersStatusAndBoardFeatures()
        {
            // W2 slice 3 contract: a killing blow sinks the view (submerged,
            // sunk tint, bars hidden, later flashes ignored); status events
            // warm the resting tint; obstacles/slow zones and the wind arrow
            // spawn from the optional board context.
            var gameObject = new GameObject("state-visuals-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "b", Side = "enemy", Position = new SimVector2 { X = 100, Y = 0 }, Heading = 180, Speed = 3, Hp = 20, Sail = 110, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "status", ShipId = "a", Status = new SimShipStatus { OnFire = true } },
                                new SimEvent { Type = "broadside", ShipId = "a", TargetShipId = "b", Hit = true, TargetRemaining = new SimRemaining { Hp = 0, Sail = 110, Crew = 50 } }
                            }
                        }
                    },
                    turnLimit: 1,
                    introLine: "state visuals test",
                    completionLine: "done",
                    obstacles: new List<SimObstacle> { new SimObstacle { Position = new SimVector2 { X = 50, Y = 0 }, Radius = 20 } },
                    slowZones: new List<SimSlowZone> { new SimSlowZone { Position = new SimVector2 { X = 70, Y = 10 }, Radius = 15, SpeedPenalty = 2 } },
                    wind: new SimWind { Direction = 0, Speed = 4 });

                // Board features spawned at sim positions scaled to world.
                var rock = spectator.transform.Find("obstacle-50-0");
                var debris = spectator.transform.Find("slow-zone-70-10");
                Assert.That(rock, Is.Not.Null);
                Assert.That(debris, Is.Not.Null);
                Assert.That(rock.position.x, Is.EqualTo(5f).Within(0.001f));
                Assert.That(debris.position.z, Is.EqualTo(1f).Within(0.001f));

                // Wind arrow exists, points downwind (heading 0 → yaw 90) and
                // anchors near the fleet centroid plus the serialized offset.
                var wind = spectator.transform.Find("wind-indicator");
                Assert.That(wind, Is.Not.Null);
                Assert.That(wind.rotation.eulerAngles.y, Is.EqualTo(90f).Within(0.01f));

                var victimView = spectator.transform.Find("marker-b").GetComponent<Armada.Client.Playback.ShipView>();
                var victimRenderer = victimView.GetComponent<Renderer>();

                spectator.Tick(0.1f);  // banner begins
                spectator.Tick(0.6f);  // banner ends
                spectator.Tick(0.1f);  // status step: ship a ablaze
                Assert.That(spectator.HudText, Does.Contain("ABLAZE"));
                var attackerView = spectator.transform.Find("marker-a").GetComponent<Armada.Client.Playback.ShipView>();
                Assert.That(attackerView.RestingColor, Is.Not.EqualTo(new Color(0.20f, 0.75f, 0.35f)));

                spectator.Tick(0.3f);  // status step ends
                spectator.Tick(0.1f);  // killing broadside begins
                Assert.That(spectator.HudText, Does.Contain("SUNK!"));
                spectator.Tick(0.5f);  // flash completes; readouts update

                Assert.That(victimView.IsSunk, Is.True);
                // Submerged below the 0.5 marker height and resting at the
                // deep-sea tint; bars hidden.
                Assert.That(victimView.transform.position.y, Is.LessThan(0.5f));
                Assert.That(victimView.RestingColor, Is.EqualTo(new Color(0.10f, 0.16f, 0.24f)));
                Assert.That(spectator.transform.Find("hull-bar-b").gameObject.activeSelf, Is.False);
                var sunkColor = victimRenderer.material.color;

                // A later flash on a sunk ship is ignored.
                spectator.Tick(0.1f);
                Assert.That(victimRenderer.material.color, Is.EqualTo(sunkColor));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator SpectatorRenderer_RakeFlourishAndMissDimReadOnTheBoard()
        {
            // W2 slice 2 feedback contract: a raked hit narrates the rake and
            // flashes the victim too; a miss flashes muted instead of the hit
            // color. Colors are asserted against the renderer's own math.
            var gameObject = new GameObject("event-feedback-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "b", Side = "enemy", Position = new SimVector2 { X = 100, Y = 0 }, Heading = 180, Speed = 3, Hp = 140, Sail = 110, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord
                        {
                            Turn = 1,
                            Events = new List<SimEvent>
                            {
                                new SimEvent { Type = "broadside", ShipId = "a", TargetShipId = "b", Hit = true, Side = "starboard", Rake = "bow", TargetRemaining = new SimRemaining { Hp = 100, Sail = 110, Crew = 50 } },
                                new SimEvent { Type = "broadside", ShipId = "b", TargetShipId = "a", Hit = false, TargetRemaining = new SimRemaining { Hp = 120, Sail = 80, Crew = 50 } }
                            }
                        }
                    },
                    turnLimit: 1,
                    introLine: "feedback test",
                    completionLine: "done");

                var attacker = spectator.transform.Find("marker-a").GetComponent<Renderer>();
                var victim = spectator.transform.Find("marker-b").GetComponent<Renderer>();

                spectator.Tick(0.1f);  // begin turn banner
                spectator.Tick(0.6f);  // finish banner
                spectator.Tick(0.1f);  // begin raked broadside
                Assert.That(spectator.HudText, Does.Contain("BOW RAKE"));
                // Rake flourish: attacker AND victim carry the shot color.
                var roundShot = new Color(1.00f, 0.72f, 0.05f);
                Assert.That(attacker.material.color, Is.EqualTo(roundShot));
                Assert.That(victim.material.color, Is.EqualTo(roundShot));

                spectator.Tick(0.5f);  // finish the flash
                spectator.Tick(0.1f);  // begin the miss
                Assert.That(spectator.HudText, Does.Contain("miss"));
                // Miss reads muted: the hit color pulled 60% toward gray.
                var dimmed = Color.Lerp(roundShot, Color.gray, 0.6f);
                Assert.That(victim.material.color.r, Is.EqualTo(dimmed.r).Within(0.001f));
                Assert.That(victim.material.color.g, Is.EqualTo(dimmed.g).Within(0.001f));
                Assert.That(victim.material.color.b, Is.EqualTo(dimmed.b).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
        }

        private sealed class MastheadViewProvider : Armada.Client.Playback.ShipViewProvider
        {
            public int Created;

            public override Armada.Client.Playback.ShipView CreateShipView(SimShip ship, Transform parent)
            {
                Created++;
                var root = new GameObject($"masthead-{ship.Id}");
                root.transform.SetParent(parent, worldPositionStays: false);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(root.transform, worldPositionStays: false);
                var view = root.AddComponent<Armada.Client.Playback.ShipView>();
                view.Configure(body.GetComponent<Renderer>(), null, 2f);
                return view;
            }
        }

        [UnityTest]
        public IEnumerator ShipViewProvider_OnTheSameGameObjectIsUsedWithoutWiring()
        {
            // The documented no-wiring extension path (Codex P2 on PR #87): a
            // provider component beside the renderer must win over the
            // primitive default even when the serialized field is unset — and
            // its reported TopClearance must drive the bar lift.
            var gameObject = new GameObject("ship-view-custom-provider-test");
            gameObject.SetActive(false);
            try
            {
                var custom = gameObject.AddComponent<MastheadViewProvider>();
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>(),
                    turnLimit: 1,
                    introLine: "custom provider test",
                    completionLine: "done");

                Assert.That(custom.Created, Is.EqualTo(1));
                Assert.That(spectator.transform.Find("masthead-player-sloop-a"), Is.Not.Null);
                var hullBar = spectator.transform.Find("hull-bar-player-sloop-a");
                Assert.That(hullBar, Is.Not.Null);
                // markerHeight 0.5 + custom TopClearance 2.0 + barClearance 0.4.
                Assert.That(hullBar.position.y, Is.EqualTo(0.5f + 2f + 0.4f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator ShipViewProvider_SpawnsDirectionalViewsWithDerivedBarClearance()
        {
            // W2 view-abstraction contract: views come from the provider seam
            // (primitives by default), carry a bow cue on local +z, yaw
            // 90 − heading so the bow tracks the engine's (cos h, sin h)
            // motion, and readout bars float at the view's reported top plus
            // the clearance gap — never a hardcoded shape height.
            var gameObject = new GameObject("ship-view-provider-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "enemy-clipper-a", Side = "enemy", Position = new SimVector2 { X = 100, Y = 0 }, Heading = 180, Speed = 3, Hp = 140, Sail = 110, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>(),
                    turnLimit: 1,
                    introLine: "view test",
                    completionLine: "done");

                var player = spectator.transform.Find("marker-player-sloop-a");
                var enemy = spectator.transform.Find("marker-enemy-clipper-a");
                Assert.That(player, Is.Not.Null);
                Assert.That(enemy, Is.Not.Null);

                // Provider seam satisfied: every marker is a ShipView with a
                // directional bow cue.
                Assert.That(player.GetComponent<Armada.Client.Playback.ShipView>(), Is.Not.Null);
                Assert.That(player.Find("bow-cue"), Is.Not.Null);
                Assert.That(enemy.Find("bow-cue"), Is.Not.Null);

                // Heading 0 moves +x in world space, so yaw must be 90; the
                // legacy yaw = heading mapping pointed the bow at +z.
                Assert.That(player.rotation.eulerAngles.y, Is.EqualTo(90f).Within(0.01f));
                Assert.That(enemy.rotation.eulerAngles.y, Is.EqualTo(270f).Within(0.01f));

                // Bar lift derives from the view: cube top 0.5 vs capsule top
                // 1.0, plus the 0.4 clearance gap over markerHeight 0.5.
                var playerHull = spectator.transform.Find("hull-bar-player-sloop-a");
                var enemyHull = spectator.transform.Find("hull-bar-enemy-clipper-a");
                Assert.That(playerHull, Is.Not.Null);
                Assert.That(enemyHull, Is.Not.Null);
                Assert.That(playerHull.position.y, Is.EqualTo(0.5f + 0.5f + 0.4f).Within(0.001f));
                Assert.That(enemyHull.position.y, Is.EqualTo(0.5f + 1f + 0.4f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator PlaybackControlsController_DrivesPauseStepAndSpeedThroughTheRendererApi()
        {
            // Inactive so neither component's Update runs; the test drives the
            // button handlers directly — the same calls the on-screen touch
            // buttons make (D2-B) — and asserts renderer state.
            var gameObject = new GameObject("playback-controls-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                var controls = gameObject.AddComponent<Armada.Client.UI.PlaybackControlsController>();
                typeof(Armada.Client.UI.PlaybackControlsController)
                    .GetField("spectator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(controls, spectator);

                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>
                    {
                        new Mission01TurnRecord { Turn = 1, Events = new List<SimEvent>() }
                    },
                    turnLimit: 1,
                    introLine: "controls test",
                    completionLine: "done");

                // Pause toggle mirrors the renderer state and its caption
                // always names the action the button performs.
                Assert.That(controls.PauseCaption, Is.EqualTo("Pause"));
                controls.OnTogglePause();
                Assert.That(spectator.IsPaused, Is.True);
                Assert.That(controls.PauseCaption, Is.EqualTo("Resume"));

                // Step arms exactly one playback step while paused.
                controls.OnStep();
                spectator.Tick(0.1f);
                Assert.That(spectator.CurrentStep, Is.Not.Null);
                Assert.That(spectator.CurrentStep.Kind, Is.EqualTo(Armada.Client.Playback.PlaybackStepKind.TurnStart));

                // Speed buttons cycle the shared presets, clamped at the ends.
                Assert.That(spectator.SpeedMultiplier, Is.EqualTo(1f));
                controls.OnSpeedUp();
                Assert.That(spectator.SpeedMultiplier, Is.EqualTo(2f));
                controls.OnSpeedUp();
                controls.OnSpeedUp();
                Assert.That(spectator.SpeedMultiplier, Is.EqualTo(4f));
                controls.OnSpeedDown();
                Assert.That(spectator.SpeedMultiplier, Is.EqualTo(2f));

                controls.OnTogglePause();
                Assert.That(spectator.IsPaused, Is.False);
                Assert.That(controls.PauseCaption, Is.EqualTo("Pause"));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            yield break;
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
                Assert.That(request.Modifiers.MutualRamming, Is.True);
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

        /// <summary>
        /// Stands in for the mission 10 resolve route. Mirrors the two
        /// behaviours the play loop depends on: every call re-resolves the
        /// whole submitted prefix, and the server always simulates through to
        /// the turn limit, resolving turns past the authored prefix with no
        /// player orders. The run "wins" once the player has authored
        /// <see cref="WinOnTurn"/> turns.
        /// </summary>
        private sealed class FakeMission10PrefixClient : IMission10Client
        {
            public const int WinOnTurn = 3;

            public readonly List<int> ResolvedPrefixLengths = new();
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
                var authored = request.Turns?.Count ?? 0;
                ResolvedPrefixLengths.Add(authored);

                var won = authored >= WinOnTurn;
                var recordCount = won ? WinOnTurn : Mission10Scenario.TurnLimit;
                var records = new List<Mission01TurnRecord>();
                for (var turn = 1; turn <= recordCount; turn++)
                {
                    var sunk = won && turn == WinOnTurn
                        ? new List<string> { "enemy-clipper-a", "enemy-clipper-b" }
                        : new List<string>();
                    records.Add(new Mission01TurnRecord
                    {
                        Turn = turn,
                        Hash = $"hash-{turn}",
                        Summary = new SimSummary
                        {
                            PlayerRemaining = 2,
                            EnemyRemaining = won && turn == WinOnTurn ? 0 : 2,
                            Sunk = sunk
                        },
                        Events = new List<SimEvent>
                        {
                            // Distinct per turn so the test can tell which
                            // record the renderer was handed.
                            new SimEvent
                            {
                                Type = "movement",
                                ShipId = "player-sloop-a",
                                Position = new SimVector2 { X = turn * 10, Y = 30 }
                            }
                        }
                    });
                }

                return Task.FromResult(new ServiceResult<Mission10Outcome>
                {
                    Data = new Mission10Outcome
                    {
                        MissionCode = Mission10Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = won ? "win" : "loss",
                        FailReason = won ? null : "timeout",
                        TurnCount = won ? WinOnTurn : Mission10Scenario.TurnLimit,
                        TurnLimit = Mission10Scenario.TurnLimit,
                        BonusObjectives = new Mission10BonusObjectives
                        {
                            SailShredder = won,
                            MixedBattery = won
                        },
                        Telemetry = new Mission10Telemetry(),
                        Turns = records
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        [UnityTest]
        public IEnumerator Mission10PlayController_AuthorsTurnByTurnAndCompletesWithTheAuthoredProof()
        {
            var missionClient = new FakeMission10PrefixClient();
            var completionClient = new FakeMissionCompletionClient();
            var flow = new Mission10Flow(missionClient, null, completionClient);

            // Inactive so Update never runs: the test drives PollPlayback and
            // the renderer's Tick itself, and MissionUIController.Start never
            // fires a network refresh.
            var gameObject = new GameObject("mission10-play-test");
            gameObject.SetActive(false);
            try
            {
                var controller = gameObject.AddComponent<Mission10PlayController>();
                var spectator = gameObject.AddComponent<SpectatorRenderer>();
                var missionUI = gameObject.AddComponent<MissionUIController>();

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

                controller.Compose(flow, spectator, missionUI, Mission10Bootstrap.PlayableSeed);
                controller.BeginMission();

                // Turn 1 opens with both sloops available to order.
                Assert.That(controller.Phase, Is.EqualTo(Mission10PlayController.PlayPhase.OrderEntry));
                Assert.That(controller.TurnNumber, Is.EqualTo(1));
                Assert.That(controller.CurrentSession.Drafts, Has.Count.EqualTo(2));

                // The opening position is on screen before the first orders
                // are written — the player is aiming at this board.
                Assert.That(spectator.TryGetMarkerPosition("player-sloop-a", out var openingPosition), Is.True);
                Assert.That(openingPosition.x, Is.EqualTo(0f).Within(0.001f));

                for (var authored = 1; authored <= FakeMission10PrefixClient.WinOnTurn; authored++)
                {
                    // Author the turn: fire chain shot at the leading clipper.
                    controller.OnCycleTarget();
                    controller.OnToggleAmmo();
                    controller.OnConfirmTurn();

                    // The fake client completes synchronously, so the awaited
                    // continuation can already have carried the phase past
                    // Resolving by the time the click handler returns; a real
                    // HTTP client yields first. Either way a submit is
                    // in flight and the phase must have left order entry.
                    Assert.That(controller.ActiveSubmit, Is.Not.Null);
                    Assert.That(
                        controller.Phase,
                        Is.EqualTo(Mission10PlayController.PlayPhase.Resolving)
                            .Or.EqualTo(Mission10PlayController.PlayPhase.Playback));
                    while (!controller.ActiveSubmit.IsCompleted)
                    {
                        yield return null;
                    }

                    Assert.That(controller.LastError, Is.Null);
                    Assert.That(controller.Phase, Is.EqualTo(Mission10PlayController.PlayPhase.Playback));

                    // Only the newest record is played back, never the whole
                    // prefix and never the ghost tail the server resolved past
                    // the authored turns.
                    var playedTurns = new HashSet<int>();
                    for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                    {
                        spectator.Tick(0.5f);
                        var step = spectator.CurrentStep;
                        if (step != null && step.Kind != PlaybackStepKind.RunComplete)
                        {
                            playedTurns.Add(step.Turn);
                        }
                    }

                    Assert.That(spectator.IsFinished, Is.True);
                    Assert.That(playedTurns, Is.EquivalentTo(new[] { authored }));

                    controller.PollPlayback();

                    if (authored == 1)
                    {
                        // Undo is free: the order array is client-side and the
                        // server holds no run state, so withdrawing a turn
                        // just shortens the next prefix.
                        Assert.That(controller.TurnNumber, Is.EqualTo(2));

                        // Turn 1's movement event carried sloop A to sim x=10,
                        // i.e. world x=1 at the placeholder 0.1 scale.
                        Assert.That(spectator.TryGetMarkerPosition("player-sloop-a", out var playedPosition), Is.True);
                        Assert.That(playedPosition.x, Is.EqualTo(1f).Within(0.001f));

                        controller.OnUndoTurn();
                        Assert.That(controller.Phase, Is.EqualTo(Mission10PlayController.PlayPhase.OrderEntry));
                        Assert.That(controller.TurnNumber, Is.EqualTo(1));

                        // The board must rewind with the order array: replacement
                        // orders are written against the opening position, not
                        // the withdrawn turn's end state.
                        Assert.That(spectator.TryGetMarkerPosition("player-sloop-a", out var rewoundPosition), Is.True);
                        Assert.That(rewoundPosition.x, Is.EqualTo(0f).Within(0.001f));

                        // Re-author the withdrawn turn so the run continues.
                        controller.OnCycleTarget();
                        controller.OnToggleAmmo();
                        controller.OnConfirmTurn();
                        while (!controller.ActiveSubmit.IsCompleted)
                        {
                            yield return null;
                        }

                        for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                        {
                            spectator.Tick(0.5f);
                        }

                        controller.PollPlayback();
                    }

                    if (authored < FakeMission10PrefixClient.WinOnTurn)
                    {
                        // Not over: the server's ghost tail past the authored
                        // prefix reported a timeout loss, which the loop must
                        // ignore.
                        Assert.That(controller.Phase, Is.EqualTo(Mission10PlayController.PlayPhase.OrderEntry));
                        Assert.That(controller.LastOutcome.Result, Is.EqualTo("loss"));
                        Assert.That(controller.TurnNumber, Is.EqualTo(authored + 1));
                    }
                }

                // The win landed inside the authored prefix, so the run is over.
                Assert.That(controller.Phase, Is.EqualTo(Mission10PlayController.PlayPhase.Finished));
                Assert.That(controller.LastOutcome.Result, Is.EqualTo("win"));

                // One resolve per confirmed turn, each re-sending the whole
                // prefix; the undone turn re-sent length 1 a second time.
                Assert.That(
                    missionClient.ResolvedPrefixLengths,
                    Is.EqualTo(new[] { 1, 1, 2, 3 }));

                // CompleteMission10 is async void; with fake clients it
                // finishes within a few frames.
                for (var frame = 0; completionClient.LastRequest == null && frame < 120; frame++)
                {
                    yield return null;
                }

                // The completion proof is the array the player authored — the
                // exact turns the winning resolve was called with.
                Assert.That(completionClient.LastCode, Is.EqualTo(Mission10Scenario.MissionCode));
                Assert.That(completionClient.LastRequest.Seed, Is.EqualTo(Mission10Bootstrap.PlayableSeed));
                Assert.That(completionClient.LastRequest.Turns, Has.Count.EqualTo(FakeMission10PrefixClient.WinOnTurn));
                Assert.That(
                    completionClient.LastRequest.Turns,
                    Is.SameAs(missionClient.LastResolveRequest.Turns));
                Assert.That(completionClient.LastRequest.Turns[0][0].Ammo, Is.EqualTo("chain"));
                Assert.That(
                    completionClient.LastRequest.Turns[0][0].TargetShipId,
                    Is.EqualTo(Mission10Scenario.EnemyShipIds[0]));
                // The mission carries no upgrade tiers.
                Assert.That(completionClient.LastRequest.Upgrades, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        [Test]
        public void ShipClassCatalog_MapsTheMissionVocabulary()
        {
            // The wire SimShip has no class field; the catalog derives class
            // from the stable mission/PvP id vocabulary and must default —
            // never fail — for anything it does not recognize.
            ShipClass Classify(string id) =>
                ShipClassCatalog.Classify(new SimShip { Id = id, Side = "enemy" });

            Assert.That(Classify("enemy-frigate-a"), Is.EqualTo(ShipClass.Frigate));
            Assert.That(Classify("alpha-frigate-b"), Is.EqualTo(ShipClass.Frigate));
            Assert.That(Classify("enemy-escort-a"), Is.EqualTo(ShipClass.Frigate));
            Assert.That(Classify("enemy-clipper-a"), Is.EqualTo(ShipClass.Clipper));
            Assert.That(Classify("enemy-brig-b"), Is.EqualTo(ShipClass.Brig));
            Assert.That(Classify("enemy-flagship"), Is.EqualTo(ShipClass.Capital));
            Assert.That(Classify("enemy-dreadnought"), Is.EqualTo(ShipClass.Capital));
            Assert.That(Classify("enemy-reinforcement"), Is.EqualTo(ShipClass.Capital));
            Assert.That(Classify("player-sloop-a"), Is.EqualTo(ShipClass.Sloop));
            Assert.That(Classify("enemy-aggressor"), Is.EqualTo(ShipClass.Sloop));
            Assert.That(Classify("enemy-corvette-a"), Is.EqualTo(ShipClass.Sloop));
            Assert.That(Classify("something-unmapped"), Is.EqualTo(ShipClass.Sloop));

            Assert.That(
                ShipClassCatalog.LiveryFor(new SimShip { Id = "x", Side = "player" }),
                Is.EqualTo(ShipLivery.Aurorian));
            Assert.That(
                ShipClassCatalog.LiveryFor(new SimShip { Id = "x", Side = "enemy" }),
                Is.EqualTo(ShipLivery.Crimson));

            Assert.That(
                ShipClassCatalog.ScaleFor(new SimShip { Id = "enemy-reinforcement", Side = "enemy" }),
                Is.EqualTo(ShipClassCatalog.ReinforcementScale));
            Assert.That(
                ShipClassCatalog.ScaleFor(new SimShip { Id = "enemy-flagship", Side = "enemy" }),
                Is.EqualTo(1f));
        }

#if UNITY_EDITOR
        // Class × livery prefab paths, mirrored from ShipViewProviderWiring
        // (editor assembly, unreachable from tests) — a rename there fails
        // these tests loudly instead of silently un-wiring scenes.
        private static readonly (string field, string path)[] GreyboxSlots =
        {
            ("sloopAurorian", "Assets/Art/Ships/Sloop/shp-sloop--aurorian.prefab"),
            ("sloopCrimson", "Assets/Art/Ships/Sloop/shp-sloop--crimson.prefab"),
            ("frigateAurorian", "Assets/Art/Ships/Frigate/shp-frigate--aurorian.prefab"),
            ("frigateCrimson", "Assets/Art/Ships/Frigate/shp-frigate--crimson.prefab"),
            ("clipperAurorian", "Assets/Art/Ships/Clipper/shp-clipper--aurorian.prefab"),
            ("clipperCrimson", "Assets/Art/Ships/Clipper/shp-clipper--crimson.prefab"),
            ("brigAurorian", "Assets/Art/Ships/Brig/shp-brig--aurorian.prefab"),
            ("brigCrimson", "Assets/Art/Ships/Brig/shp-brig--crimson.prefab"),
            ("capitalAurorian", "Assets/Art/Ships/Capital/shp-capital--aurorian.prefab"),
            ("capitalCrimson", "Assets/Art/Ships/Capital/shp-capital--crimson.prefab")
        };

        private static Armada.Client.Playback.ShipView LoadShipPrefab(string path)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Armada.Client.Playback.ShipView>(path);
        }

        private static PrefabShipViewProvider AddWiredPrefabProvider(GameObject host)
        {
            var provider = host.AddComponent<PrefabShipViewProvider>();
            var serialized = new UnityEditor.SerializedObject(provider);
            foreach (var (field, path) in GreyboxSlots)
            {
                var prefab = LoadShipPrefab(path);
                Assert.That(prefab, Is.Not.Null, $"greybox prefab missing: {path}");
                serialized.FindProperty(field).objectReferenceValue = prefab;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return provider;
        }

        [Test]
        public void GreyboxPrefabs_HonorTheShipViewContract()
        {
            foreach (var (field, path) in GreyboxSlots)
            {
                var prefab = LoadShipPrefab(path);
                Assert.That(prefab, Is.Not.Null, $"greybox prefab missing: {path}");

                // Tint/accent split: hull is the tint surface, the rig the
                // accent, and the trim renderer stays outside the contract so
                // its authored livery survives runtime recoloring.
                Assert.That(prefab.TintRenderer, Is.Not.Null, field);
                Assert.That(prefab.TintRenderer.gameObject.name, Is.EqualTo("hull"), field);
                Assert.That(prefab.transform.Find("rig"), Is.Not.Null, field);
                Assert.That(prefab.transform.Find("trim"), Is.Not.Null, field);

                // Honest TopClearance: the masthead, well above a bare hull.
                Assert.That(prefab.TopClearance, Is.GreaterThan(0.5f), field);
                Assert.That(prefab.TopClearance, Is.LessThan(2.5f), field);

                // Directional silhouette: the geometry at the far +z end (the
                // bow — or the brig's protruding ram) is much narrower than
                // the beam, so heading reads from the top-down camera.
                var hullMesh = prefab.TintRenderer.GetComponent<MeshFilter>().sharedMesh;
                var maxZ = float.MinValue;
                var beam = 0f;
                foreach (var vertex in hullMesh.vertices)
                {
                    maxZ = Mathf.Max(maxZ, vertex.z);
                    beam = Mathf.Max(beam, Mathf.Abs(vertex.x));
                }

                foreach (var vertex in hullMesh.vertices)
                {
                    if (vertex.z > maxZ - 0.01f)
                    {
                        Assert.That(Mathf.Abs(vertex.x), Is.LessThan(beam * 0.5f),
                            $"{field}: bow end must be far narrower than the beam, found width at z={vertex.z}");
                    }
                }
            }

            // Liveries are distinct where the contract allows: the authored
            // trim material differs between factions.
            var aurorian = LoadShipPrefab(GreyboxSlots[0].path);
            var crimson = LoadShipPrefab(GreyboxSlots[1].path);
            var aurorianTrim = aurorian.transform.Find("trim").GetComponent<Renderer>().sharedMaterial;
            var crimsonTrim = crimson.transform.Find("trim").GetComponent<Renderer>().sharedMaterial;
            Assert.That(aurorianTrim.color, Is.Not.EqualTo(crimsonTrim.color));

            // Class scale ordering (art-needs §2): capital dwarfs frigate,
            // frigate outsizes the sloop.
            float HullLength(int slot)
            {
                var view = LoadShipPrefab(GreyboxSlots[slot].path);
                return view.TintRenderer.GetComponent<MeshFilter>().sharedMesh.bounds.size.z;
            }

            Assert.That(HullLength(8), Is.GreaterThan(HullLength(2)));
            Assert.That(HullLength(2), Is.GreaterThan(HullLength(0)));
        }

        [UnityTest]
        public IEnumerator PrefabShipViewProvider_SpawnsClassLiveryPrefabsWithDerivedBars()
        {
            var gameObject = new GameObject("prefab-provider-test");
            gameObject.SetActive(false);
            try
            {
                AddWiredPrefabProvider(gameObject);
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 },
                        new SimShip { Id = "enemy-clipper-a", Side = "enemy", Position = new SimVector2 { X = 100, Y = 0 }, Heading = 180, Speed = 3, Hp = 140, Sail = 110, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>(),
                    turnLimit: 1,
                    introLine: "prefab provider test",
                    completionLine: "done");

                var player = spectator.transform.Find("marker-player-sloop-a");
                var enemy = spectator.transform.Find("marker-enemy-clipper-a");
                Assert.That(player, Is.Not.Null);
                Assert.That(enemy, Is.Not.Null);

                // Prefab views, not primitives: the greybox rig is present
                // and there is no primitive bow cue.
                Assert.That(player.Find("rig"), Is.Not.Null);
                Assert.That(player.Find("bow-cue"), Is.Null);

                // The renderer still owns rotation: yaw = 90 − heading.
                Assert.That(player.rotation.eulerAngles.y, Is.EqualTo(90f).Within(0.01f));
                Assert.That(enemy.rotation.eulerAngles.y, Is.EqualTo(270f).Within(0.01f));

                // The enemy clipper carries the Crimson trim material.
                var trim = enemy.Find("trim").GetComponent<Renderer>().sharedMaterial;
                var crimsonTrim = LoadShipPrefab(GreyboxSlots[5].path)
                    .transform.Find("trim").GetComponent<Renderer>().sharedMaterial;
                Assert.That(trim, Is.EqualTo(crimsonTrim));

                // Bars derive from each prefab's honest TopClearance:
                // markerHeight 0.5 + clearance + barClearance 0.4.
                var sloopClearance = LoadShipPrefab(GreyboxSlots[0].path).TopClearance;
                var clipperClearance = LoadShipPrefab(GreyboxSlots[5].path).TopClearance;
                Assert.That(
                    spectator.transform.Find("hull-bar-player-sloop-a").position.y,
                    Is.EqualTo(0.5f + sloopClearance + 0.4f).Within(0.001f));
                Assert.That(
                    spectator.transform.Find("hull-bar-enemy-clipper-a").position.y,
                    Is.EqualTo(0.5f + clipperClearance + 0.4f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PrefabShipViewProvider_FallsBackToPrimitivesWhenSlotsAreEmpty()
        {
            // A partially-arted project must always render: an unwired
            // provider behaves exactly like the primitive default.
            var gameObject = new GameObject("prefab-provider-fallback-test");
            gameObject.SetActive(false);
            try
            {
                gameObject.AddComponent<PrefabShipViewProvider>();
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>(),
                    turnLimit: 1,
                    introLine: "fallback test",
                    completionLine: "done");

                var marker = spectator.transform.Find("marker-player-sloop-a");
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.Find("bow-cue"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield break;
        }

        [Test]
        public void PrefabShipViewProvider_ScalesTheReinforcementVariant()
        {
            // The m06 reinforcement renders the capital model at hull length
            // 1.2 instead of 2.2, with TopClearance scaled to match so the
            // readout bars stay anchored to the real masthead.
            var gameObject = new GameObject("prefab-provider-scale-test");
            gameObject.SetActive(false);
            try
            {
                var provider = AddWiredPrefabProvider(gameObject);
                var capitalClearance = LoadShipPrefab(GreyboxSlots[9].path).TopClearance;
                var view = provider.CreateShipView(
                    new SimShip { Id = "enemy-reinforcement", Side = "enemy" },
                    gameObject.transform);

                Assert.That(
                    view.transform.localScale.x,
                    Is.EqualTo(ShipClassCatalog.ReinforcementScale).Within(0.001f));
                Assert.That(
                    view.TopClearance,
                    Is.EqualTo(capitalClearance * ShipClassCatalog.ReinforcementScale).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SourcedShipPrefabs_HonorTheViewContract()
        {
            // Lane C: the Kenney-sourced classes (sloop/frigate/capital ×
            // both liveries) must satisfy the same contract the greybox
            // pinned — waterline pivot, class-normalized hull length,
            // honest masthead TopClearance, hull (not a sail/flag) as the
            // tint surface.
            (string path, float length)[] sourced =
            {
                ("Assets/Art/Ships/Sloop/shp-sloop-src--aurorian.prefab", 1.0f),
                ("Assets/Art/Ships/Sloop/shp-sloop-src--crimson.prefab", 1.0f),
                ("Assets/Art/Ships/Frigate/shp-frigate-src--aurorian.prefab", 1.4f),
                ("Assets/Art/Ships/Frigate/shp-frigate-src--crimson.prefab", 1.4f),
                ("Assets/Art/Ships/Capital/shp-capital-src--aurorian.prefab", 2.2f),
                ("Assets/Art/Ships/Capital/shp-capital-src--crimson.prefab", 2.2f)
            };

            foreach (var (path, length) in sourced)
            {
                var prefab = LoadShipPrefab(path);
                Assert.That(prefab, Is.Not.Null, $"sourced prefab missing: {path}");

                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Assert.That(instance.TintRenderer, Is.Not.Null, path);
                    var tintName = instance.TintRenderer.name.ToLowerInvariant();
                    Assert.That(tintName, Does.Not.Contain("sail"), path);
                    Assert.That(tintName, Does.Not.Contain("flag"), path);

                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    // Class-normalized hull length and a waterline pivot
                    // (keel below zero, deck above).
                    Assert.That(bounds.size.z, Is.EqualTo(length).Within(0.05f), path);
                    Assert.That(bounds.min.y, Is.LessThan(0f), path);
                    Assert.That(bounds.max.y, Is.GreaterThan(0f), path);

                    // Honest masthead clearance: matches the real top.
                    Assert.That(instance.TopClearance, Is.EqualTo(bounds.max.y).Within(0.01f), path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator BoardFeatures_SpawnAuthoredPrefabsDeterministically()
        {
            // Lane B (art-needs §3 P2): wired rock/debris prefabs replace the
            // pre-art primitives; the rock variant derives from the sim
            // position (stable across spawns), the footprint scales with the
            // radius while authored height survives, and both features keep
            // their reviewed spectator-tuning tints.
            var gameObject = new GameObject("board-feature-prefab-test");
            gameObject.SetActive(false);
            try
            {
                var spectator = gameObject.AddComponent<Armada.Client.Playback.SpectatorRenderer>();
                var rockPrefabs = new[]
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Board/env-rock-a.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Board/env-rock-b.prefab"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Board/env-rock-c.prefab")
                };
                var debrisPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Board/env-debris.prefab");
                foreach (var prefab in rockPrefabs)
                {
                    Assert.That(prefab, Is.Not.Null, "board rock prefab missing");
                }

                Assert.That(debrisPrefab, Is.Not.Null, "debris prefab missing");

                var serialized = new UnityEditor.SerializedObject(spectator);
                var rocks = serialized.FindProperty("rockPrefabs");
                rocks.arraySize = rockPrefabs.Length;
                for (var i = 0; i < rockPrefabs.Length; i++)
                {
                    rocks.GetArrayElementAtIndex(i).objectReferenceValue = rockPrefabs[i];
                }

                serialized.FindProperty("debrisPrefab").objectReferenceValue = debrisPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                spectator.BeginTurns(
                    new List<SimShip>
                    {
                        new SimShip { Id = "player-sloop-a", Side = "player", Position = new SimVector2 { X = 0, Y = 0 }, Heading = 0, Speed = 3, Hp = 120, Sail = 80, Crew = 50 }
                    },
                    new List<Mission01TurnRecord>(),
                    turnLimit: 1,
                    introLine: "board feature test",
                    completionLine: "done",
                    obstacles: new List<SimObstacle> { new SimObstacle { Position = new SimVector2 { X = 50, Y = 0 }, Radius = 20 } },
                    slowZones: new List<SimSlowZone> { new SimSlowZone { Position = new SimVector2 { X = 70, Y = 10 }, Radius = 15, SpeedPenalty = 2 } });

                var rock = spectator.transform.Find("obstacle-50-0");
                Assert.That(rock, Is.Not.Null);
                // Variant is |x*31 + y| % 3 = 1550 % 3 = 2 → env-rock-c, every time.
                Assert.That(
                    rock.GetComponent<MeshFilter>().sharedMesh.name,
                    Does.Contain("env-rock-c"));
                // Footprint scales with radius (20 sim units × 0.1 × 2);
                // authored height survives.
                Assert.That(rock.localScale.x, Is.EqualTo(4f).Within(0.001f));
                Assert.That(rock.localScale.y, Is.EqualTo(1f).Within(0.001f));

                var debris = spectator.transform.Find("slow-zone-70-10");
                Assert.That(debris, Is.Not.Null);
                Assert.That(
                    debris.GetComponent<MeshFilter>().sharedMesh.name,
                    Does.Contain("env-debris"));
                Assert.That(debris.localScale.x, Is.EqualTo(3f).Within(0.001f));
                // The slow-zone tint keeps its reviewed 0.5 alpha, which the
                // transparent authored material actually honors now.
                Assert.That(debris.GetComponent<Renderer>().material.color.a, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }

            yield break;
        }

        [Test]
        public void SeaMaterial_ShipsFrozenForCaptureDeterminism()
        {
            // The serialized sea material must keep _Animate = 0: headless
            // captures render it as-is, and a stray animated save would break
            // byte-stability for every future baseline run.
            var sea = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shared/mat-sea-painterly.mat");
            Assert.That(sea, Is.Not.Null, "painterly sea material missing");
            Assert.That(sea.shader.name, Is.EqualTo("Armada/WaterPainterly"));
            Assert.That(sea.GetFloat("_Animate"), Is.EqualTo(0f));
            // The reviewed base color survives as the mid band.
            Assert.That(sea.color.r, Is.EqualTo(0.07f).Within(0.001f));
            Assert.That(sea.color.g, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(sea.color.b, Is.EqualTo(0.36f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator WaterAnimator_TurnsTheSwellOnInPlayModeOnly()
        {
            // The scene ships frozen (deterministic captures); Start — which
            // only play mode runs — flips the instance material to animated
            // without touching the shared asset.
            var gameObject = new GameObject("water-animator-test");
            gameObject.SetActive(false);
            try
            {
                var renderer = gameObject.AddComponent<MeshRenderer>();
                var material = new Material(Shader.Find("Armada/WaterPainterly"));
                renderer.sharedMaterial = material;
                Assert.That(material.GetFloat("_Animate"), Is.EqualTo(0f));

                var animator = gameObject.AddComponent<Armada.Client.Playback.WaterAnimator>();
                animator.Configure(renderer);
                gameObject.SetActive(true);
                yield return null;

                Assert.That(renderer.material.GetFloat("_Animate"), Is.EqualTo(1f));
                // The shared asset-side material stays frozen.
                Assert.That(material.GetFloat("_Animate"), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
#endif
    }
}
