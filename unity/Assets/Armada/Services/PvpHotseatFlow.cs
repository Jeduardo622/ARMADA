using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// Seam over SimService.PreviewAsync so hot-seat tests can fake the
    /// backend resolution without HTTP.
    /// </summary>
    public interface ISimPreviewClient
    {
        Task<SimPreviewResult> PreviewAsync(SimPreviewRequest request);
    }

    /// <summary>One resolved hot-seat turn plus what playback needs.</summary>
    public sealed class PvpTurnResolution
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public int Turn { get; init; }
        /// <summary>Ship snapshot from before the turn resolved; seeds the
        /// per-turn TurnPlayback remaining blocks.</summary>
        public List<SimShip> ShipsAtTurnStart { get; init; }
        public Mission01TurnRecord Record { get; init; }
        public string MatchResult { get; init; }
    }

    /// <summary>
    /// Drives the PvP hot-seat loop for the pinned 2v2 skirmish: both sides
    /// author orders at one machine, the client submits the combined order
    /// set for exactly one turn to /sim/preview, and the server-resolved
    /// nextState is chained into the following turn. Client-held match state
    /// is a hot-seat-only affordance; networked play (slice 2+) is
    /// server-authoritative.
    /// </summary>
    public sealed class PvpHotseatFlow
    {
        public const string ResultOngoing = "ongoing";
        public const string ResultSideA = "side_a";
        public const string ResultSideB = "side_b";
        public const string ResultDraw = "draw";

        private readonly ISimPreviewClient _client;

        public int Seed { get; }
        public int TurnNumber { get; private set; } = 1;
        public SimState State { get; private set; }
        public string MatchResult { get; private set; } = ResultOngoing;

        public PvpHotseatFlow(ISimPreviewClient client, int seed = PvpScenario.DefaultSeed)
        {
            _client = client;
            Seed = seed;
            State = PvpScenario.BuildInitialState();
        }

        /// <summary>Living ships on the given engine side ("player" = side A,
        /// "enemy" = side B) in the current state.</summary>
        public List<SimShip> LivingShips(string engineSide)
        {
            var ships = new List<SimShip>();
            if (State?.Ships == null)
            {
                return ships;
            }

            foreach (var ship in State.Ships)
            {
                if (ship != null && ship.Side == engineSide && ship.Hp > 0)
                {
                    ships.Add(ship);
                }
            }

            return ships;
        }

        public async Task<PvpTurnResolution> SubmitTurnAsync(List<SimOrder> sideAOrders, List<SimOrder> sideBOrders)
        {
            if (MatchResult != ResultOngoing)
            {
                return Fail("match_over");
            }

            var validationError = ValidateSideOrders(sideAOrders, "player") ?? ValidateSideOrders(sideBOrders, "enemy");
            if (validationError != null)
            {
                return Fail(validationError);
            }

            var orders = new List<SimOrder>();
            orders.AddRange(sideAOrders);
            orders.AddRange(sideBOrders);

            var request = new SimPreviewRequest
            {
                SchemaVersion = 1,
                Seed = Seed,
                Turn = TurnNumber,
                State = State,
                Orders = orders,
                Modifiers = PvpScenario.BuildModifiers()
            };

            var result = await _client.PreviewAsync(request);
            if (result?.NextState == null || result.Summary == null)
            {
                return Fail("preview_failed");
            }

            var shipsAtStart = State.Ships;
            var resolvedTurn = TurnNumber;
            var record = new Mission01TurnRecord
            {
                Turn = resolvedTurn,
                Hash = result.Hash,
                Summary = result.Summary,
                Events = result.Events
            };

            State = result.NextState;
            TurnNumber = resolvedTurn + 1;
            MatchResult = ClassifyResult(result.Summary, resolvedTurn);

            return new PvpTurnResolution
            {
                Success = true,
                Turn = resolvedTurn,
                ShipsAtTurnStart = shipsAtStart,
                Record = record,
                MatchResult = MatchResult
            };
        }

        // Mirrors pvpResultForTurn (src/sim/pvpScenario.ts): mutual
        // annihilation and the turn limit are draws; summary counts the
        // engine sides (playerRemaining = side A).
        public static string ClassifyResult(SimSummary summary, int resolvedTurn)
        {
            if (summary.PlayerRemaining == 0 && summary.EnemyRemaining == 0)
            {
                return ResultDraw;
            }
            if (summary.EnemyRemaining == 0)
            {
                return ResultSideA;
            }
            if (summary.PlayerRemaining == 0)
            {
                return ResultSideB;
            }

            return resolvedTurn >= PvpScenario.TurnLimit ? ResultDraw : ResultOngoing;
        }

        // Hot-seat fairness guard: each side may only order its own living
        // ships, and broadsides may only target the opposing side.
        private string ValidateSideOrders(List<SimOrder> orders, string engineSide)
        {
            if (orders == null)
            {
                return "orders_missing";
            }

            var shipById = new Dictionary<string, SimShip>();
            foreach (var ship in State.Ships)
            {
                shipById[ship.Id] = ship;
            }

            foreach (var order in orders)
            {
                if (order?.ShipId == null || !shipById.TryGetValue(order.ShipId, out var ship)
                    || ship.Side != engineSide || ship.Hp <= 0)
                {
                    return "order_side_mismatch";
                }

                if (order.TargetShipId != null)
                {
                    if (!shipById.TryGetValue(order.TargetShipId, out var target)
                        || target.Side == engineSide || target.Hp <= 0)
                    {
                        return "target_side_mismatch";
                    }
                }
            }

            return null;
        }

        private static PvpTurnResolution Fail(string reason)
        {
            return new PvpTurnResolution { Success = false, FailureReason = reason };
        }
    }
}
