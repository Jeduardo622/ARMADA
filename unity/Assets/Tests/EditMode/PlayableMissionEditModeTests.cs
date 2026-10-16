using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using NUnit.Framework;

namespace Armada.Client.Tests.EditMode
{
    /// <summary>
    /// Campaign building blocks: the mission catalog, the playable runners'
    /// proof path, the gated order surfaces, and the mission-agnostic battle
    /// snapshot. All plain C#, so the fakes complete synchronously and the
    /// tests block on them directly.
    /// </summary>
    public sealed class PlayableMissionEditModeTests
    {
        [Test]
        public void Catalog_ListsTheTenMissionsAgainstTheirMirroredScenarios()
        {
            var missions = PlayableMissionCatalog.All;
            Assert.That(missions, Has.Count.EqualTo(10));

            var expected = new (string code, int turnLimit, int shipCount)[]
            {
                (Mission01Scenario.MissionCode, Mission01Scenario.TurnLimit, Mission01Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission02Scenario.MissionCode, Mission02Scenario.TurnLimit, Mission02Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission03Scenario.MissionCode, Mission03Scenario.TurnLimit, Mission03Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission04Scenario.MissionCode, Mission04Scenario.TurnLimit, Mission04Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission05Scenario.MissionCode, Mission05Scenario.TurnLimit, Mission05Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission06Scenario.MissionCode, Mission06Scenario.TurnLimit, Mission06Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission07Scenario.MissionCode, Mission07Scenario.TurnLimit, Mission07Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission08Scenario.MissionCode, Mission08Scenario.TurnLimit, Mission08Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission09Scenario.MissionCode, Mission09Scenario.TurnLimit, Mission09Scenario.BuildExpectedStart(0).State.Ships.Count),
                (Mission10Scenario.MissionCode, Mission10Scenario.TurnLimit, Mission10Scenario.BuildExpectedStart(0).State.Ships.Count)
            };

            for (var i = 0; i < missions.Count; i++)
            {
                var mission = missions[i];
                Assert.That(mission.Number, Is.EqualTo(i + 1));
                Assert.That(PlayableMissionCatalog.ByNumber(i + 1), Is.SameAs(mission));
                Assert.That(mission.Code, Is.EqualTo(expected[i].code));
                Assert.That(mission.TurnLimit, Is.EqualTo(expected[i].turnLimit));
                Assert.That(mission.BuildStartState(mission.PlayableSeed).Ships, Has.Count.EqualTo(expected[i].shipCount));
                Assert.That(mission.Title, Is.Not.Empty);
                Assert.That(mission.Briefing, Is.Not.Empty);
            }

            Assert.That(PlayableMissionCatalog.ByNumber(0), Is.Null);
            Assert.That(PlayableMissionCatalog.ByNumber(11), Is.Null);
        }

        [Test]
        public void Catalog_GatesOrderSurfacesExactlyWhereTheServerAcceptsThem()
        {
            // Boarding mirrors the win-proof config in src/routes/missions.ts
            // (allowBoarding false for missions 01-02 only); chain shot is
            // Mission 10's showcase and inert anywhere else.
            foreach (var mission in PlayableMissionCatalog.All)
            {
                Assert.That(mission.AllowsBoarding, Is.EqualTo(mission.Number >= 3), mission.DisplayName);
                Assert.That(mission.AllowsChainShot, Is.EqualTo(mission.Number == 10), mission.DisplayName);
            }
        }

        [Test]
        public void Catalog_PlaysTheServerDefaultSeedsAndMission10sSweptSeed()
        {
            // Server defaults (MISSION_0X_DEFAULT_SEED) for 01-09; Mission 10
            // keeps the free-play seed chosen in docs/demo.md.
            var seeds = new[] { 101, 202, 303, 404, 505, 606, 707, 808, 909, 872 };
            for (var i = 0; i < seeds.Length; i++)
            {
                Assert.That(PlayableMissionCatalog.All[i].PlayableSeed, Is.EqualTo(seeds[i]));
            }

            Assert.That(
                PlayableMissionCatalog.PlayableSeedMission10,
                Is.EqualTo(Armada.Client.Bootstrap.Mission10Bootstrap.PlayableSeed));
        }

        [Test]
        public void Mission06Reinforcement_MirrorsTheServerShipAndArrivesOnItsTurn()
        {
            // Pinned against the same literals as tests/mission06.test.ts:
            // the server splices this ship in with no event, so the client
            // mirror is the only way the play loop can show and target it.
            var reinforcement = Mission06Scenario.BuildReinforcement();
            Assert.That(reinforcement.Id, Is.EqualTo("enemy-reinforcement"));
            Assert.That(reinforcement.Side, Is.EqualTo("enemy"));
            Assert.That(reinforcement.Position.X, Is.EqualTo(300));
            Assert.That(reinforcement.Position.Y, Is.EqualTo(80));
            Assert.That(reinforcement.Heading, Is.EqualTo(200));
            Assert.That(reinforcement.Speed, Is.EqualTo(3));
            Assert.That(reinforcement.Hp, Is.EqualTo(108));
            Assert.That(reinforcement.Sail, Is.EqualTo(70));
            Assert.That(reinforcement.Crew, Is.EqualTo(40));

            var mission = PlayableMissionCatalog.ByNumber(6);
            Assert.That(mission.ArrivalsAt(Mission06Scenario.ReinforcementTurn - 1), Is.Empty);
            Assert.That(mission.ArrivalsAt(Mission06Scenario.ReinforcementTurn), Has.Count.EqualTo(1));
            Assert.That(mission.ArrivalsAt(Mission06Scenario.ReinforcementTurn)[0].Id, Is.EqualTo("enemy-reinforcement"));

            // Bar maxima cover every ship that can appear, arrivals included.
            var start = mission.BuildStartState(mission.PlayableSeed).Ships.Count;
            Assert.That(mission.BaselineShips(mission.PlayableSeed), Has.Count.EqualTo(start + 1));
            Assert.That(PlayableMissionCatalog.ByNumber(5).BaselineShips(505), Has.Count.EqualTo(
                Mission05Scenario.BuildExpectedStart(505).State.Ships.Count));
        }

        [Test]
        public void OrderSession_BoardsATargetOnlyWhenTheMissionAllowsIt()
        {
            var start = Mission04Scenario.BuildExpectedStart(404).State;
            var battle = new MissionBattleState(start.Ships);

            var boardingSession = new PvpOrderSession(
                "Player", battle.LivingShips("player"), battle.LivingShips("enemy"),
                allowChainShot: false, allowBoarding: true);
            boardingSession.CycleTarget();
            boardingSession.ToggleBoarding();
            boardingSession.ToggleAmmo();

            var boarding = boardingSession.BuildOrders()[0];
            Assert.That(boarding.Action, Is.EqualTo("boarding"));
            Assert.That(boarding.TargetShipId, Is.EqualTo(Mission04Scenario.EnemyShipIds[0]));
            // The route schema requires a side only for broadsides, and ammo
            // is a broadside-only key; boarding orders carry neither.
            Assert.That(boarding.Side, Is.Null);
            Assert.That(boarding.Ammo, Is.Null);
            Assert.That(boardingSession.Describe(), Does.Contain("board " + Mission04Scenario.EnemyShipIds[0]));

            // Toggling back restores the broadside, still with no ammo key
            // because chain shot is off for this mission.
            boardingSession.ToggleBoarding();
            var broadside = boardingSession.BuildOrders()[0];
            Assert.That(broadside.Action, Is.EqualTo("broadside"));
            Assert.That(broadside.Side, Is.EqualTo("starboard"));
            Assert.That(broadside.Ammo, Is.Null);
            Assert.That(boardingSession.Describe(), Does.Contain("fire round at"));

            // Missions 01-02: the toggle is inert, so no boarding order can
            // ever reach a route that would reject it.
            var lockedSession = new PvpOrderSession(
                "Player", battle.LivingShips("player"), battle.LivingShips("enemy"),
                allowChainShot: false, allowBoarding: false);
            lockedSession.CycleTarget();
            lockedSession.ToggleBoarding();
            Assert.That(lockedSession.CurrentDraft.Board, Is.False);
            Assert.That(lockedSession.BuildOrders()[0].Action, Is.EqualTo("broadside"));
        }

        [Test]
        public void OrderSession_DefaultsKeepTheExistingPvpAndMission10Surface()
        {
            // PvP and the Mission 10 play scene construct sessions without the
            // new flags: chain shot stays available, boarding stays off.
            var start = Mission10Scenario.BuildExpectedStart(872).State;
            var battle = new Mission10BattleState(start.Ships);
            var session = new PvpOrderSession("Player", battle.LivingShips("player"), battle.LivingShips("enemy"));

            Assert.That(session.AllowChainShot, Is.True);
            Assert.That(session.AllowBoarding, Is.False);

            session.CycleTarget();
            session.ToggleAmmo();
            session.ToggleBoarding();
            var order = session.BuildOrders()[0];
            Assert.That(order.Action, Is.EqualTo("broadside"));
            Assert.That(order.Ammo, Is.EqualTo("chain"));
            Assert.That(session.Describe(), Does.Contain("fire chain at " + Mission10Scenario.EnemyShipIds[0]));
        }

        [Test]
        public void BattleState_FoldsStatusTicksBoardingLossesAndArrivals()
        {
            var start = Mission07Scenario.BuildExpectedStart(707).State;
            var battle = new MissionBattleState(start.Ships);
            var enemyId = Mission07Scenario.EnemyShipIds[0];
            var playerId = Mission07Scenario.PlayerShipIds[0];
            var playerCrew = battle.Ships[0].Crew;

            battle.Apply(new Mission01TurnRecord
            {
                Turn = 1,
                Events = new List<SimEvent>
                {
                    // A failed boarding still costs the boarding party.
                    new SimEvent
                    {
                        Type = "boarding",
                        ShipId = playerId,
                        TargetShipId = enemyId,
                        Success = false,
                        CrewLoss = 7,
                        TargetRemaining = new SimRemaining { Hp = 150, Sail = 60, Crew = 30 }
                    },
                    // Fire burns hull outside any attack and can sink a ship.
                    new SimEvent
                    {
                        Type = "status",
                        ShipId = enemyId,
                        Status = new SimShipStatus(),
                        Remaining = new SimRemaining { Hp = 0, Sail = 60, Crew = 30 }
                    }
                }
            });

            Assert.That(battle.Ships[0].Id, Is.EqualTo(playerId));
            Assert.That(battle.Ships[0].Crew, Is.EqualTo(playerCrew - 7));
            Assert.That(battle.LivingShips("enemy").Exists(ship => ship.Id == enemyId), Is.False);

            // Arrivals are added once, however often the arrival turn is
            // replayed (undo rebuilds the snapshot from scratch).
            var reinforcement = Mission06Scenario.BuildReinforcement();
            Assert.That(battle.AddShip(reinforcement), Is.True);
            Assert.That(battle.AddShip(reinforcement), Is.False);
            Assert.That(battle.LivingShips("enemy").Exists(ship => ship.Id == reinforcement.Id), Is.True);

            // The snapshot owns its copy: mutating the template later does
            // not reach the battle.
            reinforcement.Hp = 1;
            Assert.That(battle.LivingShips("enemy").Find(ship => ship.Id == "enemy-reinforcement").Hp, Is.EqualTo(108));
        }

        [Test]
        public void ProofRunner_MapsTheOutcomeAndCompletesWithTheSnapshottedTurns()
        {
            var client = new FakeMission04Client { Win = true, SuccessfulBoarding = true, NoShipLost = false };
            var completion = new FakeCompletionClient();
            var runner = PlayableMissionCatalog.ForMission04(new Mission04Flow(client), completion);

            // Completing before any resolve has no proof to send.
            var early = runner.CompleteAsync("player-1", null).GetAwaiter().GetResult();
            Assert.That(early.Success, Is.False);
            Assert.That(early.ErrorReason, Is.EqualTo("no_resolved_run"));
            Assert.That(completion.LastRequest, Is.Null);

            var turns = new List<List<SimOrder>>
            {
                new List<SimOrder>
                {
                    new SimOrder { ShipId = Mission04Scenario.PlayerShipIds[0], Action = "boarding", TargetShipId = Mission04Scenario.EnemyShipIds[0] }
                }
            };
            var run = runner.RunAsync(404, turns).GetAwaiter().GetResult();

            Assert.That(run.Success, Is.True);
            Assert.That(run.Outcome.MissionCode, Is.EqualTo(Mission04Scenario.MissionCode));
            Assert.That(run.Outcome.Result, Is.EqualTo("win"));
            Assert.That(run.Outcome.Bonuses, Has.Count.EqualTo(2));
            Assert.That(run.Outcome.Bonuses[0].Label, Is.EqualTo("Successful boarding"));
            Assert.That(run.Outcome.Bonuses[0].Achieved, Is.True);
            Assert.That(run.Outcome.Bonuses[1].Achieved, Is.False);
            Assert.That(run.Outcome.Stars, Is.EqualTo(2));

            // The caller's list changing after the resolve must not change
            // the proof: the runner sent (and kept) its own snapshot.
            turns.Add(new List<SimOrder>());
            turns[0][0].TargetShipId = Mission04Scenario.EnemyShipIds[1];

            var saved = runner.CompleteAsync("player-1", new Dictionary<string, object> { ["outcome"] = "win" })
                .GetAwaiter().GetResult();
            Assert.That(saved.Success, Is.True);
            Assert.That(completion.LastCode, Is.EqualTo(Mission04Scenario.MissionCode));
            Assert.That(completion.LastRequest.PlayerId, Is.EqualTo("player-1"));
            Assert.That(completion.LastRequest.Seed, Is.EqualTo(404));
            Assert.That(completion.LastRequest.Turns, Is.SameAs(client.LastRequest.Turns));
            Assert.That(completion.LastRequest.Turns, Has.Count.EqualTo(1));
            Assert.That(completion.LastRequest.Turns[0][0].TargetShipId, Is.EqualTo(Mission04Scenario.EnemyShipIds[0]));

            // A failed resolve forgets the previous proof rather than letting
            // a later completion claim it.
            client.FailResolve = true;
            var failed = runner.RunAsync(404, turns).GetAwaiter().GetResult();
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.FailureReason, Is.EqualTo("resolve_failed"));
            Assert.That(runner.CompleteAsync("player-1", null).GetAwaiter().GetResult().ErrorReason, Is.EqualTo("no_resolved_run"));
        }

        [Test]
        public void PlayableOutcome_StarsCountTheWinAndEachBonus()
        {
            var bonuses = new List<PlayableBonus>
            {
                new PlayableBonus { Label = "a", Achieved = true },
                new PlayableBonus { Label = "b", Achieved = true }
            };
            Assert.That(new PlayableMissionOutcome { Result = "win", Bonuses = bonuses }.Stars, Is.EqualTo(3));
            Assert.That(new PlayableMissionOutcome { Result = "win", Bonuses = new List<PlayableBonus>() }.Stars, Is.EqualTo(1));
            Assert.That(new PlayableMissionOutcome { Result = "loss", Bonuses = bonuses }.Stars, Is.EqualTo(0));
        }

        private sealed class FakeMission04Client : IMission04Client
        {
            public bool Win;
            public bool SuccessfulBoarding;
            public bool NoShipLost;
            public bool FailResolve;
            public Mission01ResolveRequest LastRequest;

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
                LastRequest = request;
                if (FailResolve)
                {
                    return Task.FromResult(new ServiceResult<Mission04Outcome> { Success = false, Status = HttpStatusCode.InternalServerError });
                }

                return Task.FromResult(new ServiceResult<Mission04Outcome>
                {
                    Data = new Mission04Outcome
                    {
                        MissionCode = Mission04Scenario.MissionCode,
                        Seed = request.Seed,
                        Result = Win ? "win" : "loss",
                        FailReason = Win ? null : "timeout",
                        TurnCount = 1,
                        TurnLimit = Mission04Scenario.TurnLimit,
                        BonusObjectives = new Mission04BonusObjectives
                        {
                            SuccessfulBoarding = SuccessfulBoarding,
                            NoShipLost = NoShipLost
                        },
                        Turns = new List<Mission01TurnRecord>
                        {
                            new Mission01TurnRecord { Turn = 1, Summary = new SimSummary(), Events = new List<SimEvent>() }
                        }
                    },
                    Success = true,
                    Status = HttpStatusCode.OK
                });
            }
        }

        private sealed class FakeCompletionClient : IMissionCompletionClient
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
    }
}
