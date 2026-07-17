using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission03FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission03Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 03 "Raking Shot" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side.
    /// </summary>
    public sealed class Mission03Flow
    {
        private readonly IMission03Client _client;
        private readonly DeterministicSimHooks _hooks;

        public Mission03Flow(IMission03Client client, DeterministicSimHooks hooks = null)
        {
            _client = client;
            _hooks = hooks;
        }

        public async Task<Mission03FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission03Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission03Scenario.FingerprintOf(start.Data) != Mission03Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            var resolve = await _client.ResolveMission03Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = turns ?? new List<List<SimOrder>>()
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission03Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            return new Mission03FlowResult { Success = true, Outcome = resolve.Data };
        }

        private static Mission03FlowResult Fail(string reason)
        {
            return new Mission03FlowResult { Success = false, FailureReason = reason };
        }
    }
}
