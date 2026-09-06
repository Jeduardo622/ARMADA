using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Armada.Client.Tests.EditMode
{
    public sealed class CampaignCoreTests
    {
        private static JObject Payload(object value) => JObject.Parse(JsonConvert.SerializeObject(value,
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

        [Test]
        public void ManeuverOrder_OmitsOptionalCombatFieldsOnTheWire()
        {
            var session = new PvpOrderSession("player",
                new[] { new SimShip { Id = "player-sloop", Hp = 120 } }, new SimShip[0]);
            var json = Payload(session.BuildOrders()[0]);
            Assert.That((string)json["action"], Is.EqualTo("maneuver"));
            Assert.That(json.Property("targetShipId"), Is.Null);
            Assert.That(json.Property("side"), Is.Null);
            Assert.That(json.Property("ammo"), Is.Null);
        }

        [Test]
        public void CampaignCatalog_ContainsAllTenScenariosWithFreshPlayableStates()
        {
            Assert.That(CampaignCatalog.All.Count, Is.EqualTo(10));
            var codes = new HashSet<string>();
            for (var i = 0; i < CampaignCatalog.All.Count; i++)
            {
                var mission = CampaignCatalog.All[i];
                Assert.That(mission.Number, Is.EqualTo(i + 1));
                Assert.That(codes.Add(mission.Code), Is.True);
                Assert.That(mission.StartState().Ships.Count, Is.GreaterThan(1));
                var state = mission.StartState();
                state.Ships[0].Hp = 0;
                Assert.That(mission.StartState().Ships[0].Hp, Is.GreaterThan(0));
                Assert.That(mission.ChainShotAllowed, Is.EqualTo(i == 9));
                Assert.That(mission.BoardingAllowed, Is.EqualTo(i >= 2));
            }
        }

        [Test]
        public void CampaignOrders_BoardingUsesTargetWithoutBroadsideFields()
        {
            var session = new CampaignOrderSession(Board(), true, true);
            session.CycleTarget();
            session.CycleAction();
            var json = Payload(session.BuildOrders()[0]);
            Assert.That((string)json["action"], Is.EqualTo("boarding"));
            Assert.That((string)json["targetShipId"], Is.EqualTo("enemy-brig"));
            Assert.That(json.Property("side"), Is.Null);
            Assert.That(json.Property("ammo"), Is.Null);
        }

        [Test]
        public void CampaignOrders_UnsupportedActionsStayOffAndManeuversAreBounded()
        {
            var session = new CampaignOrderSession(Board(), false, false);
            session.CycleTarget();
            session.ToggleAmmo();
            for (var i = 0; i < 20; i++) { session.AdjustTurn(1); session.AdjustSpeed(-1); }
            var order = session.BuildOrders()[0];
            Assert.That(order.TurnDelta, Is.EqualTo(90));
            Assert.That(order.SpeedDelta, Is.EqualTo(-2));
            Assert.That(order.Ammo, Is.Null);
            session.CycleAction();
            Assert.That(session.BuildOrders()[0].Action, Is.EqualTo("maneuver"));
            session.CycleAction();
            Assert.That(session.BuildOrders()[0].Action, Is.EqualTo("broadside"));
        }

        [Test]
        public void CampaignOrders_ExcludeSunkShipsAndSnapshotSubmittedOrders()
        {
            var board = Board();
            board.Ships.Add(new SimShip { Id = "sunk-friend", Side = "player", Hp = 0 });
            board.Ships.Add(new SimShip { Id = "sunk-enemy", Side = "enemy", Hp = 0 });
            var session = new CampaignOrderSession(board, true, true);
            session.CycleTarget();
            var submitted = session.BuildOrders();
            session.ToggleAmmo();
            session.AdjustTurn(1);
            Assert.That(submitted.Count, Is.EqualTo(1));
            Assert.That(submitted[0].Ammo, Is.Null);
            Assert.That(submitted[0].TurnDelta, Is.Zero);
            Assert.That(submitted[0].Side, Is.EqualTo("port"));
            session.CycleTarget();
            Assert.That(session.Current.TargetShipId, Is.Null);
        }

        private static SimState Board() => new SimState
        {
            Turn = 1,
            Wind = new SimWind { Direction = 0, Speed = 4 },
            Ships = new List<SimShip>
            {
                new SimShip { Id = "player-sloop", Side = "player", Hp = 120, Crew = 50, Position = new SimVector2 { X = 0, Y = 0 } },
                new SimShip { Id = "enemy-brig", Side = "enemy", Hp = 100, Crew = 40, Position = new SimVector2 { X = 0, Y = 80 } }
            }
        };

        [Test]
        public void CampaignFlow_RefusesToSaveAFutureForecastWin() => CampaignFlow_RefusesToSaveAFutureForecastWinAsync().GetAwaiter().GetResult();

        private async Task CampaignFlow_RefusesToSaveAFutureForecastWinAsync()
        {
            var client = new FakeCampaignClient { EndTurn = 6 };
            var flow = new CampaignMissionFlow(client, 1, "mission-01-fair-wind", 5);
            await flow.ResolveAsync(OneTurn());
            var save = await flow.CompleteAsync("qa-player");
            Assert.That(save.Success, Is.False);
            Assert.That(client.Saves, Is.Zero);
        }

        [Test]
        public void CampaignFlow_SaveRetryKeepsExactProofAndSuccessIsIdempotent() => CampaignFlow_SaveRetryKeepsExactProofAndSuccessIsIdempotentAsync().GetAwaiter().GetResult();

        private async Task CampaignFlow_SaveRetryKeepsExactProofAndSuccessIsIdempotentAsync()
        {
            var client = new FakeCampaignClient { FailFirstSave = true };
            var flow = new CampaignMissionFlow(client, 1, "mission-01-fair-wind", 5);
            var turns = OneTurn();
            await flow.ResolveAsync(turns);
            turns[0][0].TargetShipId = "caller-mutated";
            turns.Clear();
            Assert.That((await flow.CompleteAsync("qa-player")).Success, Is.False);
            Assert.That((await flow.CompleteAsync("qa-player")).Success, Is.True);
            Assert.That(client.Request.Seed, Is.EqualTo(5));
            Assert.That(client.Request.Turns[0][0].TargetShipId, Is.EqualTo("enemy-sloop"));
            Assert.That((await flow.CompleteAsync("qa-player")).Success, Is.True);
            Assert.That(client.Saves, Is.EqualTo(2));
        }

        [Test]
        public void CampaignFlow_FailedResolveInvalidatesPreviousWinningProof() => CampaignFlow_FailedResolveInvalidatesPreviousWinningProofAsync().GetAwaiter().GetResult();

        private async Task CampaignFlow_FailedResolveInvalidatesPreviousWinningProofAsync()
        {
            var client = new FakeCampaignClient();
            var flow = new CampaignMissionFlow(client, 1, "mission-01-fair-wind", 5);
            await flow.ResolveAsync(OneTurn());
            client.FailResolve = true;
            await flow.ResolveAsync(OneTurn());
            Assert.That((await flow.CompleteAsync("qa-player")).Success, Is.False);
            Assert.That(client.Saves, Is.Zero);
        }

        [Test]
        public void CampaignBattle_UsesNextPlanningSnapshotAndUndoRestoresOpening() => CampaignBattle_UsesNextPlanningSnapshotAndUndoRestoresOpeningAsync().GetAwaiter().GetResult();

        private async Task CampaignBattle_UsesNextPlanningSnapshotAndUndoRestoresOpeningAsync()
        {
            var client = new FakeCampaignClient { EndTurn = 6 };
            var battle = new CampaignBattleSession(CampaignCatalog.All[0], client);
            await battle.BeginAsync();
            battle.Orders.CycleTarget();
            await battle.SubmitAsync();
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.Playback));
            battle.FinishPlayback();
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.OrderEntry));
            Assert.That(battle.State.Wind.Direction, Is.EqualTo(90));
            Assert.That(battle.State.Ships.Exists(s => s.Id == "reinforcement"), Is.True);
            battle.Undo();
            Assert.That(battle.State.Wind.Direction, Is.EqualTo(0));
            Assert.That(battle.State.Ships.Exists(s => s.Id == "reinforcement"), Is.False);
        }

        [Test]
        public void CampaignBattle_WaitsForPlaybackAndSuccessfulSave() => CampaignBattle_WaitsForPlaybackAndSuccessfulSaveAsync().GetAwaiter().GetResult();

        private async Task CampaignBattle_WaitsForPlaybackAndSuccessfulSaveAsync()
        {
            var client = new FakeCampaignClient { FailFirstSave = true };
            var battle = new CampaignBattleSession(CampaignCatalog.All[0], client);
            await battle.BeginAsync();
            battle.Orders.CycleTarget();
            await battle.SubmitAsync();
            await battle.SaveAsync("qa-player");
            Assert.That(client.Saves, Is.Zero);
            battle.FinishPlayback();
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.Victory));
            await battle.SaveAsync("qa-player");
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.SaveFailed));
            await battle.SaveAsync("qa-player");
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.Saved));
        }

        [Test]
        public void CampaignBattle_FailedTurnPreservesEditableOrdersForRetry() => CampaignBattle_FailedTurnPreservesEditableOrdersForRetryAsync().GetAwaiter().GetResult();

        private async Task CampaignBattle_FailedTurnPreservesEditableOrdersForRetryAsync()
        {
            var client = new FakeCampaignClient { EndTurn = 6 };
            var battle = new CampaignBattleSession(CampaignCatalog.All[0], client);
            await battle.BeginAsync();
            battle.Orders.CycleTarget();
            client.FailResolve = true;
            await battle.SubmitAsync();
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.OrderEntry));
            Assert.That(battle.Orders.Current.TargetShipId, Is.EqualTo("enemy-brig"));
            client.FailResolve = false;
            await battle.SubmitAsync();
            Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.Playback));
        }

        private static List<List<SimOrder>> OneTurn() => new List<List<SimOrder>>
        {
            new List<SimOrder> { new SimOrder { ShipId = "player-sloop", Action = "broadside", TargetShipId = "enemy-sloop", Side = "port" } }
        };

        private sealed class FakeCampaignClient : ICampaignMissionClient
        {
            public int EndTurn = 1;
            public bool FailResolve;
            public bool FailFirstSave;
            public int Saves;
            public MissionCompleteRequest Request;
            public Task<CampaignRunResult> ResolveAsync(int number, int seed, List<List<SimOrder>> turns)
                => Task.FromResult(new CampaignRunResult
                {
                    Success = !FailResolve,
                    Outcome = new CampaignOutcome { MissionCode = "mission-01-fair-wind", Seed = seed, Result = "win", TurnCount = EndTurn,
                        Turns = Records() }
                });
            private List<Mission01TurnRecord> Records()
            {
                var result = new List<Mission01TurnRecord>();
                for (var i = 0; i < EndTurn; i++)
                {
                    var state = Board();
                    state.Turn = i + 1;
                    if (i > 0)
                    {
                        state.Wind.Direction = 90;
                        state.Ships.Add(new SimShip { Id = "reinforcement", Side = "enemy", Hp = 100, Position = new SimVector2() });
                    }
                    result.Add(new Mission01TurnRecord { Turn = i + 1, StartState = state, NextState = Board(), Events = new List<SimEvent>() });
                }
                return result;
            }
            public Task<CampaignSaveResult> CompleteAsync(int number, string code, MissionCompleteRequest request)
            {
                Saves++;
                Request = request;
                return Task.FromResult(new CampaignSaveResult { Success = !FailFirstSave || Saves > 1 });
            }
        }
    }
}

