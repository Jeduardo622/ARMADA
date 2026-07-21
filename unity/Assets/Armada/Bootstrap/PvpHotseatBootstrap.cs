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
    /// Runtime composition root for the PvP hot-seat demo. Composes the
    /// authenticated client service graph, builds a PvpHotseatFlow over
    /// SimService, and hands the order-authoring UI its collaborators. Both
    /// captains share this one client; match state between turns is held
    /// client-side, which is a hot-seat-only affordance.
    /// </summary>
    public sealed class PvpHotseatBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private PvpHotseatUIController orderUI;
        [SerializeField] private Playback.SpectatorRenderer spectator;

        [Header("Run")]
        [SerializeField] private int seed = PvpScenario.DefaultSeed;

        private AuthService _authService;
        private PvpHotseatFlow _flow;

        // Composition happens in Awake so the wired collaborators are in
        // place before any sibling Start runs; Unity does not guarantee
        // sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[PvpHotseatBootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph; two
            // roots in one scene would race guest sessions for the same UI
            // wiring, so composition roots refuse to run together.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission08Bootstrap>() != null || FindFirstObjectByType<Mission09Bootstrap>() != null || FindFirstObjectByType<Mission10Bootstrap>() != null)
            {
                Debug.LogError("[PvpHotseatBootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
                return;
            }

            determinism?.ApplyFixedTimestep();
            determinism?.ApplySeed(seed);

            var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var flags = new FeatureFlags(clientConfig.FeatureToggles);

            AuthService authServiceRef = null;
            var proxy = new AuthProxy(() => authServiceRef?.GetTokenAsync());
            var apiClient = new ApiClient(clientConfig.BaseUrl, proxy, json);
            var authService = new AuthService(apiClient, json);
            authServiceRef = authService;

            var simService = new SimService(apiClient, flags, json);

            _authService = authService;
            _flow = new PvpHotseatFlow(simService, seed);

            if (orderUI != null)
            {
                orderUI.Compose(_flow, spectator);
            }
        }

        private async void Start()
        {
            if (_flow == null || orderUI == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            orderUI.BeginMatch();
        }

        private sealed class AuthProxy : IAuthProvider
        {
            private readonly Func<Task<string>> _resolver;
            public AuthProxy(Func<Task<string>> resolver) => _resolver = resolver;
            public Task<string> GetTokenAsync() => _resolver();
        }
    }
}
