using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission06FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission06Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 06 "Dreadnought Siege" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side.
    /// </summary>
    public sealed class Mission06Flow
    {
        private readonly IMission06Client _client;
        private readonly DeterministicSimHooks _hooks;

        public Mission06Flow(IMission06Client client, DeterministicSimHooks hooks = null)
        {
            _client = client;
            _hooks = hooks;
        }

        public async Task<Mission06FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission06Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission06Scenario.FingerprintOf(start.Data) != Mission06Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            var resolve = await _client.ResolveMission06Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = turns ?? new List<List<SimOrder>>()
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission06Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            return new Mission06FlowResult { Success = true, Outcome = resolve.Data };
        }

        private static Mission06FlowResult Fail(string reason)
        {
            return new Mission06FlowResult { Success = false, FailureReason = reason };
        }
    }
}
