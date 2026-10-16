using System;
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
    /// Runtime composition root for the Campaign scene: builds the
    /// authenticated client service graph once, hands the campaign a factory
    /// that builds a fresh runner for whichever mission the player picks, and
    /// opens the mission list once the guest session is ready.
    ///
    /// Each runner wraps that mission's own client flow (scenario-parity,
    /// seed, and outcome checks included) and completes wins through the
    /// flow-aware proof path, so every mission saves exactly as its
    /// dedicated bootstrap would.
    /// </summary>
    public sealed class CampaignBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private CampaignController campaign;
        [SerializeField] private MissionPlayController playUI;
        [SerializeField] private MissionUIController missionUI;
        [SerializeField] private Playback.SpectatorRenderer spectator;

        private AuthService _authService;
        private MissionService _missionService;
        private UpgradesService _upgradesService;

        // Composition happens in Awake so the wired [SerializeField] services
        // are in place before any MissionUIController.Start can run its first
        // refresh; Unity does not guarantee sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[CampaignBootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph. A
            // second graph would open a second guest session with a different
            // player and race it for the same UI wiring, so composition
            // roots must not run together in one scene.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission08Bootstrap>() != null || FindFirstObjectByType<Mission09Bootstrap>() != null || FindFirstObjectByType<Mission10Bootstrap>() != null || FindFirstObjectByType<PvpHotseatBootstrap>() != null || FindFirstObjectByType<PvpNetplayBootstrap>() != null)
            {
                Debug.LogError("[CampaignBootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
                return;
            }

            var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var flags = new FeatureFlags(clientConfig.FeatureToggles);

            AuthService authServiceRef = null;
            var proxy = new AuthProxy(() => authServiceRef?.GetTokenAsync());
            var apiClient = new ApiClient(clientConfig.BaseUrl, proxy, json);
            var authService = new AuthService(apiClient, json);
            authServiceRef = authService;

            _authService = authService;
            _missionService = new MissionService(apiClient, flags);
            _upgradesService = new UpgradesService(apiClient, flags);

            WireUI(_missionService, authService);
        }

        private async void Start()
        {
            if (_missionService == null)
            {
                return;
            }

            if (campaign == null || playUI == null)
            {
                Debug.LogError("[CampaignBootstrap] Campaign needs a CampaignController and a MissionPlayController; staying idle.");
                return;
            }

            await _authService.GetTokenAsync();

            campaign.Compose(CreateRunner, spectator, missionUI, playUI);
            campaign.ShowMissionSelect();
        }

        /// <summary>Fresh runner per launch: a runner's completion proof
        /// belongs to the run it resolved, never to a previous mission.</summary>
        private IPlayableMissionRunner CreateRunner(int missionNumber)
        {
            return PlayableMissionCatalog.CreateRunner(missionNumber, _missionService, determinism, _upgradesService);
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
