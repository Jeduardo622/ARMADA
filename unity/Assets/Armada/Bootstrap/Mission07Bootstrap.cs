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
    /// Runtime composition root for the Mission 07 "Burning Seas" slice.
    /// Constructs the client service graph, runs Mission07Flow with a
    /// deterministic seed and the pinned gunnery orders, then reports a win
    /// through MissionUIController.CompleteMission07 so the completion proof
    /// re-sends the resolved run's snapshotted seed, turns, and owned upgrade
    /// tiers.
    /// </summary>
    public sealed class Mission07Bootstrap : MonoBehaviour
    {
        // Seed 21 wins the pinned gunnery orders both with and without owned
        // upgrade tiers (tests/mission07.test.ts).
        public const int DefaultSeed = 21;

        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private MissionUIController missionUI;

        [Header("Run")]
        [SerializeField] private int seed = DefaultSeed;

        private AuthService _authService;
        private Mission07Flow _flow;

        // Composition happens in Awake so the wired [SerializeField] services
        // are in place before any MissionUIController.Start can run its first
        // refresh; Unity does not guarantee sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[Mission07Bootstrap] Missing client config asset.");
                return;
            }

            // ArmadaBootstrap already composes an authenticated service
            // graph. A second graph would open a second guest session with a
            // different player and race it for the same UI wiring, so the
            // two bootstraps must not run together.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null)
            {
                Debug.LogError("[Mission07Bootstrap] ArmadaBootstrap is active in the scene; refusing to compose a second authenticated service graph.");
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
            var upgradesService = new UpgradesService(apiClient, flags);

            _authService = authService;
            _flow = new Mission07Flow(missionService, determinism, upgradesService, missionService);

            WireUI(missionService, authService);
        }

        private async void Start()
        {
            if (_flow == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            await DriveAsync(_flow, missionUI, seed, BuildGunneryOrders());
        }

        /// <summary>
        /// Runs the mission and, on a win, completes it through the UI
        /// controller. Completion must go through the flow-aware
        /// CompleteMission07 path so the request carries the exact seed,
        /// turns, and tiers the run was resolved with.
        /// </summary>
        public static async Task<Mission07FlowResult> DriveAsync(Mission07Flow flow, MissionUIController missionUI, int seed, List<List<SimOrder>> turns)
        {
            var run = await flow.RunAsync(seed, turns);
            if (run.Success && run.Outcome?.Result == "win" && missionUI != null)
            {
                missionUI.CompleteMission07(flow, new Dictionary<string, object> { ["outcome"] = "win" });
            }

            return run;
        }

        /// <summary>
        /// Client-side mirror of the pure-gunnery order fixture pinned in
        /// tests/mission07.test.ts: both sloops focus frigate A for the first
        /// five turns then frigate B, heaving to (-2 speed) from turn 4.
        /// </summary>
        public static List<List<SimOrder>> BuildGunneryOrders()
        {
            var turns = new List<List<SimOrder>>(Mission07Scenario.TurnLimit);
            for (var i = 0; i < Mission07Scenario.TurnLimit; i++)
            {
                var target = i < 5 ? Mission07Scenario.EnemyShipIds[0] : Mission07Scenario.EnemyShipIds[1];
                var speedDelta = i >= 3 ? -2 : 0;
                turns.Add(new List<SimOrder>
                {
                    Fire(Mission07Scenario.PlayerShipIds[0], target, speedDelta),
                    Fire(Mission07Scenario.PlayerShipIds[1], target, speedDelta)
                });
            }

            return turns;
        }

        private static SimOrder Fire(string shipId, string targetShipId, int speedDelta)
        {
            return new SimOrder
            {
                ShipId = shipId,
                Action = "broadside",
                TargetShipId = targetShipId,
                Side = "starboard",
                TurnDelta = 0,
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
