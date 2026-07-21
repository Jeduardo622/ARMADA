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
    /// Runtime composition root for the networked PvP demo. Composes the
    /// authenticated client service graph, builds a PvpNetplayFlow over
    /// PvpMatchService, and hands the netplay UI its collaborators. Each
    /// running client (editor Play Mode or the standalone build) is one
    /// authenticated player; two clients play a match through the
    /// server-authoritative pvp_api routes.
    /// </summary>
    public sealed class PvpNetplayBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring")]
        [SerializeField] private PvpNetplayUIController netplayUI;
        [SerializeField] private Playback.SpectatorRenderer spectator;

        private AuthService _authService;
        private PvpNetplayFlow _flow;

        // Composition happens in Awake so the wired collaborators are in
        // place before any sibling Start runs; Unity does not guarantee
        // sibling Start ordering.
        private void Awake()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[PvpNetplayBootstrap] Missing client config asset.");
                return;
            }

            // Each bootstrap composes an authenticated service graph; two
            // roots in one scene would race guest sessions for the same UI
            // wiring, so composition roots refuse to run together.
            if (FindFirstObjectByType<ArmadaBootstrap>() != null || FindFirstObjectByType<Mission07Bootstrap>() != null || FindFirstObjectByType<Mission08Bootstrap>() != null || FindFirstObjectByType<Mission09Bootstrap>() != null || FindFirstObjectByType<Mission10Bootstrap>() != null || FindFirstObjectByType<PvpHotseatBootstrap>() != null)
            {
                Debug.LogError("[PvpNetplayBootstrap] Another composition root is active in the scene; refusing to compose a second authenticated service graph.");
                return;
            }

            determinism?.ApplyFixedTimestep();
            determinism?.ApplySeed();

            var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var flags = new FeatureFlags(clientConfig.FeatureToggles);

            AuthService authServiceRef = null;
            var proxy = new AuthProxy(() => authServiceRef?.GetTokenAsync());
            var apiClient = new ApiClient(clientConfig.BaseUrl, proxy, json);
            var authService = new AuthService(apiClient, json);
            authServiceRef = authService;

            var matchService = new PvpMatchService(apiClient, flags);

            _authService = authService;
            _flow = new PvpNetplayFlow(matchService);

            if (netplayUI != null)
            {
                netplayUI.Compose(_flow, spectator);
            }
        }

        private async void Start()
        {
            if (_flow == null || netplayUI == null)
            {
                return;
            }

            await _authService.GetTokenAsync();
            netplayUI.ShowMenu();
        }

        private sealed class AuthProxy : IAuthProvider
        {
            private readonly Func<Task<string>> _resolver;
            public AuthProxy(Func<Task<string>> resolver) => _resolver = resolver;
            public Task<string> GetTokenAsync() => _resolver();
        }
    }
}
