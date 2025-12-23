using System;
using System.Text.Json;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Armada.Client.UI;
using UnityEngine;

namespace Armada.Client.Bootstrap
{
    public sealed class ArmadaBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private DeterministicSimHooks determinism;

        [Header("UI Wiring (optional)")]
        [SerializeField] private MissionUIController missionUI;
        [SerializeField] private InventoryUIController inventoryUI;

        private FeatureFlags _flags;
        private ApiClient _apiClient;
        private AuthService _authService;
        private ConfigService _configService;
        private MissionService _missionService;
        private InventoryService _inventoryService;
        private SimService _simService;
        private TelemetryService _telemetry;
        private TelemetryQueue _telemetryQueue;
        private JsonSerializerOptions _json;

        private void Awake()
        {
            determinism?.ApplyFixedTimestep();
            determinism?.ApplySeed();
        }

        private async void Start()
        {
            if (clientConfig == null)
            {
                Debug.LogError("[Bootstrap] Missing client config asset.");
                return;
            }

            _json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            _flags = new FeatureFlags(clientConfig.FeatureToggles);

            AuthService authServiceRef = null;
            var proxy = new AuthProxy(() => authServiceRef?.GetTokenAsync());
            _apiClient = new ApiClient(clientConfig.BaseUrl, proxy, _json);

            _authService = new AuthService(_apiClient, _json);
            authServiceRef = _authService;
            _configService = new ConfigService(_apiClient, clientConfig.ConfigSigningKey, _json);
            _missionService = new MissionService(_apiClient, _flags);
            _inventoryService = new InventoryService(_apiClient, _flags);
            _simService = new SimService(_apiClient, _flags, _json);

            _telemetryQueue = new TelemetryQueue(_json, clientConfig.TelemetryMaxPayloadBytes);
            _telemetry = new TelemetryService(_apiClient, _telemetryQueue, clientConfig.TelemetryFlushSeconds, clientConfig.TelemetryMaxBatchSize, clientConfig.TelemetryMaxPayloadBytes, null);

            WireUI();
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await _authService.GetTokenAsync();
            if (_authService.CurrentPlayer != null)
            {
                _telemetry = new TelemetryService(_apiClient, _telemetryQueue, clientConfig.TelemetryFlushSeconds, clientConfig.TelemetryMaxBatchSize, clientConfig.TelemetryMaxPayloadBytes, _authService.CurrentPlayer.Id);
                _telemetry.Start();
                _telemetry.Enqueue("session_start", null);
            }

            await _configService.FetchAsync("client");

            if (missionUI != null)
            {
                await missionUI.RefreshAsync();
            }

            if (inventoryUI != null)
            {
                await inventoryUI.RefreshAsync();
            }
        }

        private void WireUI()
        {
            if (missionUI != null)
            {
                missionUI.GetType().GetField("missionService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(missionUI, _missionService);
                missionUI.GetType().GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(missionUI, _authService);
            }

            if (inventoryUI != null)
            {
                inventoryUI.GetType().GetField("inventoryService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(inventoryUI, _inventoryService);
                inventoryUI.GetType().GetField("authService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(inventoryUI, _authService);
            }
        }

        private void OnDestroy()
        {
            _telemetry?.Enqueue("session_end", null);
            _telemetry?.Dispose();
        }

        private sealed class AuthProxy : IAuthProvider
        {
            private readonly Func<Task<string>> _resolver;
            public AuthProxy(Func<Task<string>> resolver) => _resolver = resolver;
            public Task<string> GetTokenAsync() => _resolver();
        }
    }
}

