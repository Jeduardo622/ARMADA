using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission09FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission09Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 09 "Iron Bow" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side. The mission does not support upgrade tiers, so requests
    /// never carry an upgrades block and stay byte-identical to the legacy
    /// payload shape.
    /// </summary>
    public sealed class Mission09Flow
    {
        private readonly IMission09Client _client;
        private readonly DeterministicSimHooks _hooks;
        private readonly IMissionCompletionClient _completionClient;

        private int _resolvedSeed;
        private List<List<SimOrder>> _resolvedTurns;
        private bool _hasResolvedRun;

        public Mission09Flow(
            IMission09Client client,
            DeterministicSimHooks hooks = null,
            IMissionCompletionClient completionClient = null)
        {
            _client = client;
            _hooks = hooks;
            _completionClient = completionClient;
        }

        public async Task<Mission09FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            _hasResolvedRun = false;

            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission09Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission09Scenario.FingerprintOf(start.Data) != Mission09Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            // Snapshot the turns so later caller mutations cannot desync the
            // completion proof from the turns the run was resolved with.
            var runTurns = SnapshotTurns(turns);
            var resolve = await _client.ResolveMission09Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = runTurns
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission09Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            _resolvedSeed = seed;
            _resolvedTurns = runTurns;
            _hasResolvedRun = true;

            return new Mission09FlowResult { Success = true, Outcome = resolve.Data };
        }

        /// <summary>
        /// Completes the last resolved run, re-sending the exact seed and
        /// turns the win was resolved with so the server-side proof
        /// re-simulates the same outcome.
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

            return await _completionClient.CompleteAsync(Mission09Scenario.MissionCode, new MissionCompleteRequest
            {
                PlayerId = playerId,
                Result = result,
                BestScore = bestScore,
                Seed = _resolvedSeed,
                Turns = _resolvedTurns
            });
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

        private static Mission09FlowResult Fail(string reason)
        {
            return new Mission09FlowResult { Success = false, FailureReason = reason };
        }
    }
}
