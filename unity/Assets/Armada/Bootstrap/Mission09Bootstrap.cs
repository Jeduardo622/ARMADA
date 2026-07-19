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
    /// Runtime composition root for the Mission 09 "Iron Bow" slice.
    /// Constructs the client service graph, runs Mission09Flow with a
    /// deterministic seed and the pinned ramming orders, then reports a win
    /// through MissionUIController.CompleteMission09 so the completion proof
    /// re-sends the resolved run's snapshotted seed and turns.
    /// </summary>
    public sealed class Mission09Bootstrap : MonoBehaviour
    {
        // Seed 87 wins the pinned ramming orders with a turn-4 double ram and
        // both bonuses (tests/mission09.test.ts).
        public const int DefaultSeed = 87;

        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private MissionUIController missionUI;

        [Header("Run")]
        [SerializeField] private int seed = DefaultSeed;

        private AuthService _authService;
        private Mission09Flow _flow;

        // Composition happens in Awake so the wired [SerializeField] services
        // are in place before any MissionUIController.Start can run its first
        // refresh; Unity does not guarantee sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[Mission09Bootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph. A
            // second graph would open a second guest session with a different
            // player and race it for the same UI wiring, so composition
            // roots must not run together in one scene.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission08Bootstrap>() != null)
            {
                Debug.LogError("[Mission09Bootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
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
            _flow = new Mission09Flow(missionService, determinism, missionService);

            WireUI(missionService, authService);
        }

        private async void Start()
        {
            if (_flow == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            await DriveAsync(_flow, missionUI, seed, BuildRammingOrders());
        }

        /// <summary>
        /// Runs the mission and, on a win, completes it through the UI
        /// controller. Completion must go through the flow-aware
        /// CompleteMission09 path so the request carries the exact seed and
        /// turns the run was resolved with.
        /// </summary>
        public static async Task<Mission09FlowResult> DriveAsync(Mission09Flow flow, MissionUIController missionUI, int seed, List<List<SimOrder>> turns)
        {
            var run = await flow.RunAsync(seed, turns);
            if (run.Success && run.Outcome?.Result == "win" && missionUI != null)
            {
                missionUI.CompleteMission09(flow, new Dictionary<string, object> { ["outcome"] = "win" });
            }

            return run;
        }

        /// <summary>
        /// Client-side mirror of the ramming order fixture pinned in
        /// tests/mission09.test.ts: both sloops crowd on sail for two turns
        /// (+2 speed) and drive straight downwind into the brig line, guns
        /// firing on brig A for the first five turns then brig B.
        /// </summary>
        public static List<List<SimOrder>> BuildRammingOrders()
        {
            var turns = new List<List<SimOrder>>(Mission09Scenario.TurnLimit);
            for (var i = 0; i < Mission09Scenario.TurnLimit; i++)
            {
                var target = i < 5 ? Mission09Scenario.EnemyShipIds[0] : Mission09Scenario.EnemyShipIds[1];
                var speedDelta = i < 2 ? 2 : 0;
                turns.Add(new List<SimOrder>
                {
                    Fire(Mission09Scenario.PlayerShipIds[0], target, 0, speedDelta),
                    Fire(Mission09Scenario.PlayerShipIds[1], target, 0, speedDelta)
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
