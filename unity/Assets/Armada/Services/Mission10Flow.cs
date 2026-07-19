using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission10FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission10Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 10 "Sail-Cutter" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side. The mission does not support upgrade tiers, so requests
    /// never carry an upgrades block; orders carry the optional ammo key only
    /// when a chain-shot load is selected.
    /// </summary>
    public sealed class Mission10Flow
    {
        private readonly IMission10Client _client;
        private readonly DeterministicSimHooks _hooks;
        private readonly IMissionCompletionClient _completionClient;

        private int _resolvedSeed;
        private List<List<SimOrder>> _resolvedTurns;
        private bool _hasResolvedRun;

        public Mission10Flow(
            IMission10Client client,
            DeterministicSimHooks hooks = null,
            IMissionCompletionClient completionClient = null)
        {
            _client = client;
            _hooks = hooks;
            _completionClient = completionClient;
        }

        public async Task<Mission10FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            _hasResolvedRun = false;

            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission10Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission10Scenario.FingerprintOf(start.Data) != Mission10Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            // Snapshot the turns so later caller mutations cannot desync the
            // completion proof from the turns the run was resolved with.
            var runTurns = SnapshotTurns(turns);
            var resolve = await _client.ResolveMission10Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = runTurns
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission10Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            _resolvedSeed = seed;
            _resolvedTurns = runTurns;
            _hasResolvedRun = true;

            return new Mission10FlowResult { Success = true, Outcome = resolve.Data };
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

            return await _completionClient.CompleteAsync(Mission10Scenario.MissionCode, new MissionCompleteRequest
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
                        Side = order.Side,
                        Ammo = order.Ammo
                    });
                }

                snapshot.Add(orders);
            }

            return snapshot;
        }

        private static Mission10FlowResult Fail(string reason)
        {
            return new Mission10FlowResult { Success = false, FailureReason = reason };
        }
    }
}
