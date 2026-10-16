using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using Armada.Client.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Armada.Client.Tests.PlayMode
{
    /// <summary>
    /// The campaign's mission-agnostic play loop and its menu, driven with a
    /// fake runner that honours the prefix contract every mission's /resolve
    /// keeps: re-sending a longer prefix returns the same records for the
    /// turns already resolved, and the server simulates idle turns past the
    /// authored prefix.
    /// </summary>
    public sealed class MissionPlayPlayModeTests
    {
        private const string PlayerId = "22222222-2222-2222-2222-222222222222";

        [UnityTest]
        public IEnumerator MissionPlay_BoardsInMission04AndCompletesTheWinWithStars()
        {
            var mission = PlayableMissionCatalog.ByNumber(4);
            var runner = new FakePrefixRunner(mission, winOnTurn: 2);

            var gameObject = new GameObject("mission-play-test");
            gameObject.SetActive(false);
            try
            {
                var controller = gameObject.AddComponent<MissionPlayController>();
                var spectator = gameObject.AddComponent<SpectatorRenderer>();
                var missionUI = AuthedMissionUI(gameObject);
                var ammo = new GameObject("ammo-button");
                var board = new GameObject("board-button");
                SetField(controller, "ammoControl", ammo);
                SetField(controller, "boardingControl", board);

                PlayableMissionInfo finishedMission = null;
                PlayableMissionOutcome finishedOutcome = null;
                var finishedCount = 0;
                controller.MissionFinished += (info, outcome) =>
                {
                    finishedMission = info;
                    finishedOutcome = outcome;
                    finishedCount++;
                };

                controller.Compose(mission, runner, spectator, missionUI);
                controller.BeginMission();

                // Mission 04 offers boarding but not chain shot.
                Assert.That(ammo.activeSelf, Is.False);
                Assert.That(board.activeSelf, Is.True);
                Assert.That(controller.Phase, Is.EqualTo(MissionPlayController.PlayPhase.OrderEntry));
                Assert.That(controller.TurnNumber, Is.EqualTo(1));
                Assert.That(controller.CurrentSession.Drafts, Has.Count.EqualTo(2));
                Assert.That(controller.CurrentSession.AllowBoarding, Is.True);
                Assert.That(controller.CurrentSession.AllowChainShot, Is.False);

                // The opening position is on screen before the first orders.
                Assert.That(spectator.TryGetMarkerPosition(Mission04Scenario.PlayerShipIds[0], out _), Is.True);

                for (var authored = 1; authored <= runner.WinOnTurn; authored++)
                {
                    // Sloop A boards the lead frigate.
                    controller.OnCycleTarget();
                    controller.OnToggleBoarding();
                    controller.OnConfirmTurn();

                    Assert.That(controller.ActiveSubmit, Is.Not.Null);
                    while (!controller.ActiveSubmit.IsCompleted)
                    {
                        yield return null;
                    }

                    Assert.That(controller.LastError, Is.Null);
                    Assert.That(controller.Phase, Is.EqualTo(MissionPlayController.PlayPhase.Playback));
                    for (var tick = 0; tick < 200 && !spectator.IsFinished; tick++)
                    {
                        spectator.Tick(0.5f);
                    }

                    Assert.That(spectator.IsFinished, Is.True);
                    controller.PollPlayback();

                    if (authored < runner.WinOnTurn)
                    {
                        // The idle tail past the prefix reported a loss; the
                        // loop must ignore it and open the next turn.
                        Assert.That(controller.Phase, Is.EqualTo(MissionPlayController.PlayPhase.OrderEntry));
                        Assert.That(controller.LastOutcome.Result, Is.EqualTo("loss"));
                    }
                }

                Assert.That(controller.Phase, Is.EqualTo(MissionPlayController.PlayPhase.Finished));
                Assert.That(controller.LastOutcome.Result, Is.EqualTo("win"));
                Assert.That(runner.ResolvedPrefixLengths, Is.EqualTo(new[] { 1, 2 }));

                // Every authored turn was a boarding order with no side or ammo.
                var firstOrder = runner.LastTurns[0][0];
                Assert.That(firstOrder.ShipId, Is.EqualTo(Mission04Scenario.PlayerShipIds[0]));
                Assert.That(firstOrder.Action, Is.EqualTo("boarding"));
                Assert.That(firstOrder.TargetShipId, Is.EqualTo(Mission04Scenario.EnemyShipIds[0]));
                Assert.That(firstOrder.Side, Is.Null);
                Assert.That(firstOrder.Ammo, Is.Null);

                Assert.That(finishedCount, Is.EqualTo(1));
                Assert.That(finishedMission, Is.SameAs(mission));
                Assert.That(finishedOutcome.Stars, Is.EqualTo(2));

                // The win saves through the runner with the authed player.
                Assert.That(controller.CompletionTask, Is.Not.Null);
                while (!controller.CompletionTask.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(controller.CompletionTask.Result.Success, Is.True);
                Assert.That(runner.CompletedPlayerId, Is.EqualTo(PlayerId));
                Assert.That(runner.CompletedResult["stars"], Is.EqualTo(2));

                // Retry starts the same mission over from the opening turn.
                controller.OnRetryMission();
                Assert.That(controller.Phase, Is.EqualTo(MissionPlayController.PlayPhase.OrderEntry));
                Assert.That(controller.TurnNumber, Is.EqualTo(1));
                Assert.That(controller.LastOutcome, Is.Null);

                Object.Destroy(ammo);
                Object.Destroy(board);
            }
            finally
            {
                Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator MissionPlay_SplicesInMission06sReinforcementAndUndoesIt()
        {
            var mission = PlayableMissionCatalog.ByNumber(6);
            var runner = new FakePrefixRunner(mission, winOnTurn: Mission06Scenario.TurnLimit + 1);
            var arrivalTurn = Mission06Scenario.ReinforcementTurn;

            var gameObject = new GameObject("mission-play-arrival-test");
            gameObject.SetActive(false);
            try
            {
                var controller = gameObject.AddComponent<MissionPlayController>();
                controller.Compose(mission, runner, spectator: null, missionUI: null);
                controller.BeginMission();

                // With no renderer the loop advances straight to the next
                // order round after each resolve.
                for (var authored = 1; authored < arrivalTurn; authored++)
                {
                    Assert.That(HasShip(controller, "enemy-reinforcement"), Is.False, $"turn {authored}");
                    controller.OnConfirmTurn();
                    while (!controller.ActiveSubmit.IsCompleted)
                    {
                        yield return null;
                    }
                }

                // The server splices the reinforcement in before resolving
                // its turn, so it must be orderable-against that very turn.
                Assert.That(controller.TurnNumber, Is.EqualTo(arrivalTurn));
                Assert.That(HasShip(controller, "enemy-reinforcement"), Is.True);
                var targets = new List<string>();
                for (var i = 0; i < 3; i++)
                {
                    controller.OnCycleTarget();
                    targets.Add(controller.CurrentSession.CurrentDraft.TargetShipId);
                }

                Assert.That(targets, Does.Contain("enemy-reinforcement"));

                // Undoing back past the arrival removes it with the turn.
                controller.OnUndoTurn();
                Assert.That(controller.TurnNumber, Is.EqualTo(arrivalTurn - 1));
                Assert.That(HasShip(controller, "enemy-reinforcement"), Is.False);

                // Re-confirming brings it back exactly once.
                controller.OnConfirmTurn();
                while (!controller.ActiveSubmit.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(controller.TurnNumber, Is.EqualTo(arrivalTurn));
                var count = 0;
                foreach (var ship in controller.BattleShips)
                {
                    if (ship.Id == "enemy-reinforcement")
                    {
                        count++;
                    }
                }

                Assert.That(count, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Campaign_SelectsBriefsLaunchesAndRecordsTheBestStars()
        {
            var runners = new List<FakePrefixRunner>();
            var root = new GameObject("campaign-test");
            root.SetActive(false);
            var menu = new GameObject("menu");
            var battle = new GameObject("battle");
            var result = new GameObject("result");
            try
            {
                var campaign = root.AddComponent<CampaignController>();
                var play = root.AddComponent<MissionPlayController>();
                SetField(campaign, "menuPanel", menu);
                SetField(campaign, "battleControls", battle);
                SetField(campaign, "resultControls", result);

                campaign.Compose(
                    number =>
                    {
                        var runner = new FakePrefixRunner(PlayableMissionCatalog.ByNumber(number), winOnTurn: 1);
                        runners.Add(runner);
                        return runner;
                    },
                    spectator: null,
                    missionUI: null,
                    play: play);

                campaign.ShowMissionSelect();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.MissionSelect));
                Assert.That(menu.activeSelf, Is.True);
                Assert.That(battle.activeSelf, Is.False);
                Assert.That(result.activeSelf, Is.False);

                // Set Sail needs a selection first.
                campaign.OnSetSail();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.MissionSelect));
                Assert.That(runners, Is.Empty);

                campaign.OnSelectMission(99);
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.MissionSelect));

                campaign.OnSelectMission(9);
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.Briefing));
                Assert.That(campaign.SelectedMission.Number, Is.EqualTo(9));

                campaign.OnSetSail();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.InMission));
                Assert.That(menu.activeSelf, Is.False);
                Assert.That(battle.activeSelf, Is.True);
                Assert.That(runners, Has.Count.EqualTo(1));
                Assert.That(play.Mission.Number, Is.EqualTo(9));
                Assert.That(play.Phase, Is.EqualTo(MissionPlayController.PlayPhase.OrderEntry));

                // Leaving mid-battle is not offered.
                campaign.OnReturnToMissions();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.InMission));

                play.OnConfirmTurn();
                while (!play.ActiveSubmit.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(play.Phase, Is.EqualTo(MissionPlayController.PlayPhase.Finished));
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.Result));
                Assert.That(battle.activeSelf, Is.False);
                Assert.That(result.activeSelf, Is.True);
                Assert.That(campaign.BestStars(9), Is.EqualTo(play.LastOutcome.Stars));
                Assert.That(campaign.BestStars(9), Is.GreaterThan(0));

                // Retry replays the same mission with the same runner.
                campaign.OnRetry();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.InMission));
                Assert.That(play.TurnNumber, Is.EqualTo(1));
                Assert.That(runners, Has.Count.EqualTo(1));

                play.OnConfirmTurn();
                while (!play.ActiveSubmit.IsCompleted)
                {
                    yield return null;
                }

                campaign.OnReturnToMissions();
                Assert.That(campaign.Phase, Is.EqualTo(CampaignController.CampaignPhase.MissionSelect));
                Assert.That(menu.activeSelf, Is.True);
                Assert.That(result.activeSelf, Is.False);
                Assert.That(campaign.BestStars(9), Is.GreaterThan(0));
                Assert.That(campaign.BestStars(1), Is.EqualTo(0));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(menu);
                Object.Destroy(battle);
                Object.Destroy(result);
            }
        }

        private static bool HasShip(MissionPlayController controller, string shipId)
        {
            foreach (var ship in controller.BattleShips)
            {
                if (ship.Id == shipId)
                {
                    return true;
                }
            }

            return false;
        }

        private static MissionUIController AuthedMissionUI(GameObject host)
        {
            var missionUI = host.AddComponent<MissionUIController>();
            var authService = new AuthService(null, null);
            typeof(AuthService)
                .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(authService, new AuthState
                {
                    Token = "test-token",
                    Player = new Player { Id = PlayerId }
                });
            SetField(missionUI, "authService", authService);
            return missionUI;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        /// <summary>
        /// Resolves any mission's prefix: losses (the idle tail times out)
        /// until <see cref="WinOnTurn"/> turns are authored, then a win at
        /// that turn with the first bonus achieved. Each record moves the
        /// first player ship so playback has something to show.
        /// </summary>
        private sealed class FakePrefixRunner : IPlayableMissionRunner
        {
            private readonly PlayableMissionInfo _mission;

            public FakePrefixRunner(PlayableMissionInfo mission, int winOnTurn)
            {
                _mission = mission;
                WinOnTurn = winOnTurn;
            }

            public int WinOnTurn { get; }
            public readonly List<int> ResolvedPrefixLengths = new();
            public List<List<SimOrder>> LastTurns;
            public string CompletedPlayerId;
            public Dictionary<string, object> CompletedResult;

            public Task<PlayableRunResult> RunAsync(int seed, List<List<SimOrder>> turns)
            {
                LastTurns = PlayableMissionCatalog.CopyTurns(turns);
                var authored = turns?.Count ?? 0;
                ResolvedPrefixLengths.Add(authored);

                var won = authored >= WinOnTurn;
                var recordCount = won ? WinOnTurn : _mission.TurnLimit;
                var playerShip = _mission.BuildStartState(seed).Ships.Find(ship => ship.Side == "player");
                var records = new List<Mission01TurnRecord>();
                for (var turn = 1; turn <= recordCount; turn++)
                {
                    records.Add(new Mission01TurnRecord
                    {
                        Turn = turn,
                        Hash = $"hash-{turn}",
                        Summary = new SimSummary
                        {
                            PlayerRemaining = 1,
                            EnemyRemaining = won && turn == WinOnTurn ? 0 : 1,
                            Sunk = new List<string>()
                        },
                        Events = new List<SimEvent>
                        {
                            new SimEvent
                            {
                                Type = "movement",
                                ShipId = playerShip.Id,
                                Position = new SimVector2 { X = playerShip.Position.X + turn * 10, Y = playerShip.Position.Y }
                            }
                        }
                    });
                }

                return Task.FromResult(new PlayableRunResult
                {
                    Success = true,
                    Outcome = new PlayableMissionOutcome
                    {
                        MissionCode = _mission.Code,
                        Seed = seed,
                        Result = won ? "win" : "loss",
                        FailReason = won ? null : "timeout",
                        TurnCount = won ? WinOnTurn : _mission.TurnLimit,
                        TurnLimit = _mission.TurnLimit,
                        Turns = records,
                        Bonuses = new List<PlayableBonus>
                        {
                            new PlayableBonus { Label = "first", Achieved = true },
                            new PlayableBonus { Label = "second", Achieved = false }
                        }
                    }
                });
            }

            public Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string playerId, Dictionary<string, object> result)
            {
                CompletedPlayerId = playerId;
                CompletedResult = result;
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
