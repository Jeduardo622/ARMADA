using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission05FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission05Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 05 "Line Break" client flow: applies the
    /// deterministic seed through DeterministicSimHooks, starts the mission,
    /// verifies scenario parity against the client pin, then resolves the run
    /// server-side.
    /// </summary>
    public sealed class Mission05Flow
    {
        private readonly IMission05Client _client;
        private readonly DeterministicSimHooks _hooks;

        public Mission05Flow(IMission05Client client, DeterministicSimHooks hooks = null)
        {
            _client = client;
            _hooks = hooks;
        }

        public async Task<Mission05FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission05Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission05Scenario.FingerprintOf(start.Data) != Mission05Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            var resolve = await _client.ResolveMission05Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = turns ?? new List<List<SimOrder>>()
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission05Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            return new Mission05FlowResult { Success = true, Outcome = resolve.Data };
        }

        private static Mission05FlowResult Fail(string reason)
        {
            return new Mission05FlowResult { Success = false, FailureReason = reason };
        }
    }
}
