using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission07FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission07Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 07 "Burning Seas" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side. When an upgrades client is provided, the player's owned
    /// tiers are fetched once per run and attached to the resolve request and
    /// the matching complete request; the server re-simulates the win proof
    /// with those tiers, so both requests must carry identical values. A
    /// failed tier fetch degrades to an unupgraded run instead of blocking
    /// the mission.
    /// </summary>
    public sealed class Mission07Flow
    {
        private readonly IMission07Client _client;
        private readonly DeterministicSimHooks _hooks;
        private readonly IUpgradesClient _upgradesClient;
        private readonly IMissionCompletionClient _completionClient;

        private int _resolvedSeed;
        private List<List<SimOrder>> _resolvedTurns;
        private SimShipUpgrades _resolvedUpgrades;
        private bool _hasResolvedRun;

        public Mission07Flow(
            IMission07Client client,
            DeterministicSimHooks hooks = null,
            IUpgradesClient upgradesClient = null,
            IMissionCompletionClient completionClient = null)
        {
            _client = client;
            _hooks = hooks;
            _upgradesClient = upgradesClient;
            _completionClient = completionClient;
        }

        public async Task<Mission07FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            _hasResolvedRun = false;

            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission07Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission07Scenario.FingerprintOf(start.Data) != Mission07Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            // Snapshot the turns so later caller mutations cannot desync the
            // completion proof from the turns the run was resolved with.
            var runTurns = SnapshotTurns(turns);
            var upgrades = await FetchOwnedTiersAsync();
            var resolve = await _client.ResolveMission07Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = runTurns,
                Upgrades = upgrades
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission07Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            _resolvedSeed = seed;
            _resolvedTurns = runTurns;
            _resolvedUpgrades = upgrades;
            _hasResolvedRun = true;

            return new Mission07FlowResult { Success = true, Outcome = resolve.Data };
        }

        /// <summary>
        /// Completes the last resolved run, re-sending the exact seed, turns,
        /// and upgrade tiers the win was resolved with; mismatched tiers would
        /// change the server-side proof outcome.
        /// </summary>
        public async Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string playerId, Dictionary<string, object> result, int? bestScore = null)
        {
            if (_completionClient == null)
            {
                return new ServiceResult<MissionCompleteResponse> { Success = false, ErrorReason = "completion_client_missing" };
            }
            if (!_hasResolvedRun)
            {
                return new ServiceResult<MissionCompleteResponse> { Success = false, ErrorReason = "no_resolved_run" };
            }

            return await _completionClient.CompleteAsync(Mission07Scenario.MissionCode, new MissionCompleteRequest
            {
                PlayerId = playerId,
                Result = result,
                BestScore = bestScore,
                Seed = _resolvedSeed,
                Turns = _resolvedTurns,
                Upgrades = _resolvedUpgrades
            });
        }

        private async Task<SimShipUpgrades> FetchOwnedTiersAsync()
        {
            if (_upgradesClient == null)
            {
                return null;
            }

            var owned = await _upgradesClient.GetUpgradesAsync();
            if (!owned.Success || owned.Data == null)
            {
                return null;
            }

            return MapOwnedTiers(owned.Data.Owned);
        }

        /// <summary>
        /// Maps owned component tiers onto the sim upgrade payload. Returns
        /// null when nothing is owned so mission requests stay byte-identical
        /// to the legacy payloads.
        /// </summary>
        public static SimShipUpgrades MapOwnedTiers(List<OwnedUpgrade> owned)
        {
            if (owned == null)
            {
                return null;
            }

            var upgrades = new SimShipUpgrades();
            foreach (var entry in owned)
            {
                if (entry == null || entry.Tier <= 0)
                {
                    continue;
                }

                switch (entry.Component)
                {
                    case "cannon":
                        upgrades.Cannon = entry.Tier;
                        break;
                    case "sail":
                        upgrades.Sail = entry.Tier;
                        break;
                    case "hull":
                        upgrades.Hull = entry.Tier;
                        break;
                }
            }

            if (upgrades.Cannon == 0 && upgrades.Sail == 0 && upgrades.Hull == 0)
            {
                return null;
            }

            return upgrades;
        }

        private static List<List<SimOrder>> SnapshotTurns(List<List<SimOrder>> turns)
        {
            var snapshot = new List<List<SimOrder>>();
            if (turns == null)
            {
                return snapshot;
            }

            foreach (var turn in turns)
            {
                if (turn == null)
                {
                    snapshot.Add(null);
                    continue;
                }

                var orders = new List<SimOrder>(turn.Count);
                foreach (var order in turn)
                {
                    orders.Add(order == null ? null : new SimOrder
                    {
                        ShipId = order.ShipId,
                        Action = order.Action,
                        TargetShipId = order.TargetShipId,
                        TurnDelta = order.TurnDelta,
                        SpeedDelta = order.SpeedDelta,
                        Side = order.Side
                    });
                }

                snapshot.Add(orders);
            }

            return snapshot;
        }

        private static Mission07FlowResult Fail(string reason)
        {
            return new Mission07FlowResult { Success = false, FailureReason = reason };
        }
    }
}
