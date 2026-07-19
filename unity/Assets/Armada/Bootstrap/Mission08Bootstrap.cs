using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Armada.Client.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Armada.Client.Bootstrap
{
    /// <summary>
    /// Runtime composition root for the Mission 08 "Eye of the Wind" slice.
    /// Constructs the client service graph, runs Mission08Flow with a
    /// deterministic seed and the pinned tacking orders, then reports a win
    /// through MissionUIController.CompleteMission08 so the completion proof
    /// re-sends the resolved run's snapshotted seed and turns.
    /// </summary>
    public sealed class Mission08Bootstrap : MonoBehaviour
    {
        // Seed 9 wins the pinned tacking orders with clamped-maneuver
        // telemetry (tests/mission08.test.ts).
        public const int DefaultSeed = 9;

        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private MissionUIController missionUI;

        [Header("Run")]
        [SerializeField] private int seed = DefaultSeed;

        private AuthService _authService;
        private Mission08Flow _flow;

        // Composition happens in Awake so the wired [SerializeField] services
        // are in place before any MissionUIController.Start can run its first
        // refresh; Unity does not guarantee sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[Mission08Bootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph. A
            // second graph would open a second guest session with a different
            // player and race it for the same UI wiring, so composition
            // roots must not run together in one scene.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission09Bootstrap>() != null)
            {
                Debug.LogError("[Mission08Bootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
                return;
            }

            var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var flags = new FeatureFlags(clientConfig.FeatureToggles);

            AuthService authServiceRef = null;
            var proxy = new AuthProxy(() => authServiceRef?.GetTokenAsync());
            var apiClient = new ApiClient(clientConfig.BaseUrl, proxy, json);
            var authService = new AuthService(apiClient, json);
            authServiceRef = authService;

            var missionService = new MissionService(apiClient, flags);

            _authService = authService;
            _flow = new Mission08Flow(missionService, determinism, missionService);

            WireUI(missionService, authService);
        }

        private async void Start()
        {
            if (_flow == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            await DriveAsync(_flow, missionUI, seed, BuildTackingOrders());
        }

        /// <summary>
        /// Runs the mission and, on a win, completes it through the UI
        /// controller. Completion must go through the flow-aware
        /// CompleteMission08 path so the request carries the exact seed and
        /// turns the run was resolved with.
        /// </summary>
        public static async Task<Mission08FlowResult> DriveAsync(Mission08Flow flow, MissionUIController missionUI, int seed, List<List<SimOrder>> turns)
        {
            var run = await flow.RunAsync(seed, turns);
            if (run.Success && run.Outcome?.Result == "win" && missionUI != null)
            {
                missionUI.CompleteMission08(flow, new Dictionary<string, object> { ["outcome"] = "win" });
            }

            return run;
        }

        /// <summary>
        /// Client-side mirror of the tacking order fixture pinned in
        /// tests/mission08.test.ts: both sloops focus corvette A for the
        /// first five turns then corvette B, ordering a hard 60° weave on
        /// turns 2-3 (which the upwind clamp cuts to 30°) and heaving to
        /// (-2 speed) from turn 4.
        /// </summary>
        public static List<List<SimOrder>> BuildTackingOrders()
        {
            var turns = new List<List<SimOrder>>(Mission08Scenario.TurnLimit);
            for (var i = 0; i < Mission08Scenario.TurnLimit; i++)
            {
                var target = i < 5 ? Mission08Scenario.EnemyShipIds[0] : Mission08Scenario.EnemyShipIds[1];
                var turnDelta = i == 1 ? 60 : i == 2 ? -60 : 0;
                var speedDelta = i >= 3 ? -2 : 0;
                turns.Add(new List<SimOrder>
                {
                    Fire(Mission08Scenario.PlayerShipIds[0], target, turnDelta, speedDelta),
                    Fire(Mission08Scenario.PlayerShipIds[1], target, turnDelta, speedDelta)
                });
            }

            return turns;
        }

        private static SimOrder Fire(string shipId, string targetShipId, int turnDelta, int speedDelta)
        {
            return new SimOrder
            {
                ShipId = shipId,
                Action = "broadside",
                TargetShipId = targetShipId,
                Side = "starboard",
                TurnDelta = turnDelta,
                SpeedDelta = speedDelta
            };
        }

        private void WireUI(MissionService missionService, AuthService authService)
        {
            if (missionUI == null)
            {
                return;
            }

            missionUI.GetType().GetField("missionService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.SetValue(missionUI, missionService);
            missionUI.GetType().GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.SetValue(missionUI, authService);
        }

        private sealed class AuthProxy : IAuthProvider
        {
            private readonly Func<Task<string>> _resolver;
            public AuthProxy(Func<Task<string>> resolver) => _resolver = resolver;
            public Task<string> GetTokenAsync() => _resolver();
        }
    }
}
