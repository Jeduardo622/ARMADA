using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class Mission01FlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public Mission01Outcome Outcome { get; init; }
    }

    /// <summary>
    /// Drives the Mission 01 "Fair Wind" client flow: applies the deterministic
    /// seed through DeterministicSimHooks, starts the mission, verifies scenario
    /// parity against the client pin, then resolves the run server-side.
    /// </summary>
    public sealed class Mission01Flow
    {
        private readonly IMission01Client _client;
        private readonly DeterministicSimHooks _hooks;

        public Mission01Flow(IMission01Client client, DeterministicSimHooks hooks = null)
        {
            _client = client;
            _hooks = hooks;
        }

        public async Task<Mission01FlowResult> RunAsync(int seed, List<List<SimOrder>> turns)
        {
            if (_hooks != null)
            {
                _hooks.ApplySeed(seed);
            }

            var start = await _client.StartMission01Async(seed);
            if (!start.Success || start.Data == null)
            {
                return Fail("start_failed");
            }
            if (start.Data.Seed != seed)
            {
                return Fail("seed_mismatch");
            }
            if (Mission01Scenario.FingerprintOf(start.Data) != Mission01Scenario.Fingerprint())
            {
                return Fail("scenario_mismatch");
            }

            var resolve = await _client.ResolveMission01Async(new Mission01ResolveRequest
            {
                Seed = seed,
                Turns = turns ?? new List<List<SimOrder>>()
            });
            if (!resolve.Success || resolve.Data == null)
            {
                return Fail("resolve_failed");
            }
            if (resolve.Data.Seed != seed || resolve.Data.MissionCode != Mission01Scenario.MissionCode)
            {
                return Fail("outcome_mismatch");
            }

            return new Mission01FlowResult { Success = true, Outcome = resolve.Data };
        }

        private static Mission01FlowResult Fail(string reason)
        {
            return new Mission01FlowResult { Success = false, FailureReason = reason };
        }
    }
}
