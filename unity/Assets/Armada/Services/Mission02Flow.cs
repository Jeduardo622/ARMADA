using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission02FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission02Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 02 "Weather Gage" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side.
    /// </summary>
    public sealed class Mission02Flow
    {
        private readonly IMission02Client _client;
        private readonly DeterministicSimHooks _hooks;

        public Mission02Flow(IMission02Client client, DeterministicSimHooks hooks = null)
        {
            _client = client;
            _hooks = hooks;
        }

        public async Task<Mission02FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission02Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission02Scenario.FingerprintOf(start.Data) != Mission02Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            var resolve = await _client.ResolveMission02Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = turns ?? new List<List<SimOrder>>()
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission02Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            return new Mission02FlowResult { Success = true, Outcome = resolve.Data };
        }

        private static Mission02FlowResult Fail(string reason)
        {
            return new Mission02FlowResult { Success = false, FailureReason = reason };
        }
    }
}
