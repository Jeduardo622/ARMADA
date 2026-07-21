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
    /// Runtime composition root for the Mission 10 "Sail-Cutter" slice.
    /// Constructs the client service graph, runs Mission10Flow with a
    /// deterministic seed and the pinned mixed-battery orders, then reports a
    /// win through MissionUIController.CompleteMission10 so the completion
    /// proof re-sends the resolved run's snapshotted seed and turns.
    /// </summary>
    public sealed class Mission10Bootstrap : MonoBehaviour
    {
        // Seed 2 wins the pinned mixed-battery orders at turn 8 with both
        // bonuses (tests/mission10.test.ts).
        public const int DefaultSeed = 2;

        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private MissionUIController missionUI;

        // Optional spectate-only renderer (SpectatorDemo scene). When wired,
        // the resolved run's turn event stream plays back visually after the
        // flow completes; when null the bootstrap behaves exactly as before.
        [Header("Spectator (optional)")]
        [SerializeField] private Playback.SpectatorRenderer spectator;

        [Header("Run")]
        [SerializeField] private int seed = DefaultSeed;

        private AuthService _authService;
        private Mission10Flow _flow;

        // Composition happens in Awake so the wired [SerializeField] services
        // are in place before any MissionUIController.Start can run its first
        // refresh; Unity does not guarantee sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[Mission10Bootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph. A
            // second graph would open a second guest session with a different
            // player and race it for the same UI wiring, so composition
            // roots must not run together in one scene.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission08Bootstrap>() != null || FindFirstObjectByType<Mission09Bootstrap>() != null || FindFirstObjectByType<PvpHotseatBootstrap>() != null)
            {
                Debug.LogError("[Mission10Bootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
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
            _flow = new Mission10Flow(missionService, determinism, missionService);

            WireUI(missionService, authService);
        }

        private async void Start()
        {
            if (_flow == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            var run = await DriveAsync(_flow, missionUI, seed, BuildMixedBatteryOrders());
            if (spectator != null)
            {
                spectator.Begin(run);
            }
        }

        /// <summary>
        /// Runs the mission and, on a win, completes it through the UI
        /// controller. Completion must go through the flow-aware
        /// CompleteMission10 path so the request carries the exact seed and
        /// turns the run was resolved with.
        /// </summary>
        public static async Task<Mission10FlowResult> DriveAsync(Mission10Flow flow, MissionUIController missionUI, int seed, List<List<SimOrder>> turns)
        {
            var run = await flow.RunAsync(seed, turns);
            if (run.Success && run.Outcome?.Result == "win" && missionUI != null)
            {
                missionUI.CompleteMission10(flow, new Dictionary<string, object> { ["outcome"] = "win" });
            }

            return run;
        }

        /// <summary>
        /// Client-side mirror of the mixed-battery order fixture pinned in
        /// tests/mission10.test.ts: both sloops fire chain shot into the
        /// rigging for the first three turns while the lines close, then ball
        /// to sink, on clipper A for the first five turns then clipper B.
        /// Round-shot turns omit the ammo key so those orders stay
        /// byte-identical to the legacy payload shape.
        /// </summary>
        public static List<List<SimOrder>> BuildMixedBatteryOrders()
        {
            var turns = new List<List<SimOrder>>(Mission10Scenario.TurnLimit);
            for (var i = 0; i < Mission10Scenario.TurnLimit; i++)
            {
                var target = i < 5 ? Mission10Scenario.EnemyShipIds[0] : Mission10Scenario.EnemyShipIds[1];
                var ammo = i < 3 ? "chain" : null;
                turns.Add(new List<SimOrder>
                {
                    Fire(Mission10Scenario.PlayerShipIds[0], target, ammo),
                    Fire(Mission10Scenario.PlayerShipIds[1], target, ammo)
                });
            }

            return turns;
        }

        private static SimOrder Fire(string shipId, string targetShipId, string ammo)
        {
            return new SimOrder
            {
                ShipId = shipId,
                Action = "broadside",
                TargetShipId = targetShipId,
                Side = "starboard",
                TurnDelta = 0,
                SpeedDelta = 0,
                Ammo = ammo
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
