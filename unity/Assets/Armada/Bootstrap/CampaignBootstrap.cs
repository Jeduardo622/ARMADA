using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using Armada.Client.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Armada.Client.Bootstrap
{
    /// <summary>One authenticated identity and one campaign navigation owner per scene.</summary>
    public sealed class CampaignBootstrap : MonoBehaviour
    {
        [SerializeField] private ArmadaClientConfig clientConfig;
        [SerializeField] private Sprite harborArt;
        [SerializeField] private CampaignUIController view;
        [SerializeField] private SpectatorRenderer spectator;
        [SerializeField] private CampaignPlayController play;
        private AuthService _auth;
        private MissionService _missions;
        private InventoryService _inventory;
        private CampaignProgressService _progress;
        private UpgradesService _upgrades;
        private readonly HashSet<string> _completed = new();
        private readonly Dictionary<string, int?> _stars = new();
        private List<InventoryItem> _items = new();
        private bool _loading;
        public string PlayerId => _auth?.CurrentPlayer?.Id;
        public Task ActiveOperation { get; private set; } = Task.CompletedTask;

        private void Awake()
        {
            if (view == null) view = gameObject.AddComponent<CampaignUIController>();
            view.Initialize(harborArt);
            if (clientConfig == null || spectator == null || play == null)
            {
                view.ShowMessage("PORT CLOSED", "The campaign scene is missing required configuration.", null);
                return;
            }
            var json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var flags = new FeatureFlags(clientConfig.FeatureToggles);
            var api = new ApiClient(clientConfig.BaseUrl, new AuthProxy(() => _auth?.GetTokenAsync()), json);
            _auth = new AuthService(api, json);
            _missions = new MissionService(api, flags);
            _inventory = new InventoryService(api, flags);
            _progress = new CampaignProgressService(api);
            _upgrades = new UpgradesService(api, flags);
            play.enabled = false;
        }
        private void Start() { if (_auth != null) GoHarbor(); }
        public void GoHarbor() { if (!_loading) ActiveOperation = LoadHarborAsync(); }
        private async Task LoadHarborAsync()
        {
            _loading = true;
            play.enabled = false;
            view.ShowMessage("WELCOME ABOARD", "Opening your captain's log…", null);
            try
            {
                var token = await _auth.GetTokenAsync();
                if (this == null) return;
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(PlayerId))
                {
                    var message = _auth.LastError == "storage_unsupported"
                        ? "Secure captain profiles are not yet available on this platform."
                        : _auth.LastError == "session_invalid"
                            ? "This captain's sign-in could not be restored. Your saved profile has been kept."
                            : "Your captain's log could not be opened. Check your connection and retry.";
                    view.ShowMessage("UNABLE TO SIGN IN", message, GoHarbor);
                    return;
                }
                var progress = await _progress.GetAsync();
                var inventory = await _inventory.ListAsync(PlayerId);
                if (this == null) return;
                if (!progress.Success || progress.Data?.Progress == null || !inventory.Success || inventory.Data == null)
                {
                    view.ShowMessage("LOGBOOK UNAVAILABLE", "Your saved progress and supplies could not be loaded. Retry to continue.", GoHarbor);
                    return;
                }
                _completed.Clear();
                _stars.Clear();
                foreach (var row in progress.Data.Progress.Where(p => p.Status == "COMPLETED" && p.MissionCode != null))
                {
                    _completed.Add(row.MissionCode);
                    _stars[row.MissionCode] = row.VerifiedStars;
                }
                _items = inventory.Data;
                view.ShowHarbor(Resources(), ShowChart, ShowShipyard);
            }
            catch (Exception) { if (this != null) view.ShowMessage("CONNECTION LOST", "Your saved captain is kept. Retry when the connection returns.", GoHarbor); }
            finally { _loading = false; }
        }
        private string Resources() => $"GOLD {Quantity("gold")}     TIMBER {Quantity("timber")}     ORE {Quantity("ore")}     MISSIONS {_completed.Count} / 10";
        private int Quantity(string key) => _items.Where(i => i.ItemKey == key).Sum(i => i.Quantity);
        public void ShowChart() { if (!_loading) view.ShowChart(_completed, ShowBriefing, GoHarbor, _stars); }
        public void ShowBriefing(int number)
        {
            if (_loading || number < 1 || number > CampaignCatalog.All.Count) return;
            var mission = CampaignCatalog.All[number - 1];
            if (number > 1 && !_completed.Contains(mission.Code) && !_completed.Contains(CampaignCatalog.All[number - 2].Code)) return;
            view.ShowBriefing(mission, () => Launch(number), ShowChart);
        }
        public void Launch(int number)
        {
            if (_loading || number < 1 || number > CampaignCatalog.All.Count) return;
            var mission = CampaignCatalog.All[number - 1];
            if (number > 1 && !_completed.Contains(mission.Code) && !_completed.Contains(CampaignCatalog.All[number - 2].Code)) return;
            var session = new CampaignBattleSession(mission, new CampaignMissionClient(_missions, _upgrades));
            play.Compose(view, spectator, session, () => PlayerId, GoHarbor, next =>
            {
                // Only reached from the saved-victory screen. Navigation still refreshes durable progress at harbor.
                _completed.Add(mission.Code);
                _stars[mission.Code] = Math.Max(_stars.TryGetValue(mission.Code, out var rating) ? rating ?? 0 : 0, session.Stars);
                if (next > CampaignCatalog.All.Count) ShowChart(); else ShowBriefing(next);
            });
            play.enabled = true;
            play.BeginMission();
        }
        public void ShowShipyard() { if (!_loading) ActiveOperation = LoadShipyardAsync(null); }
        private async Task LoadShipyardAsync(string notice)
        {
            _loading = true;
            view.ShowMessage("SHIPYARD", "Checking stores and available fittings…", null);
            try
            {
                var catalog = await _upgrades.GetUpgradesAsync();
                var inventory = await _inventory.ListAsync(PlayerId);
                if (this == null) return;
                if (!catalog.Success || catalog.Data?.Catalog == null || !inventory.Success || inventory.Data == null)
                {
                    view.ShowMessage("SHIPYARD UNAVAILABLE", "The shipwright could not read your fittings and supplies.", ShowShipyard, GoHarbor);
                    return;
                }
                _items = inventory.Data;
                view.ShowShipyard(catalog.Data, _items, notice, Purchase, GoHarbor);
            }
            catch (Exception) { if (this != null) view.ShowMessage("CONNECTION LOST", "The shipyard could not be loaded.", ShowShipyard, GoHarbor); }
            finally { _loading = false; }
        }
        private void Purchase(string component, int tier)
        {
            if (!_loading) ActiveOperation = PurchaseAsync(component, tier);
        }
        private async Task PurchaseAsync(string component, int tier)
        {
            _loading = true;
            view.ShowMessage("FITTING YOUR SHIP", "The shipwright is confirming your purchase…", null);
            var notice = "The purchase could not be confirmed. Your fittings and balance have been refreshed below.";
            try
            {
                // Use exactly the tier displayed by the button. Never retry by buying a newly computed next tier.
                var result = await _upgrades.PurchaseAsync(new UpgradePurchaseRequest { PlayerId = PlayerId, Component = component, Tier = tier });
                if (result.Success) notice = "Fitting installed. Your ship is ready.";
            }
            catch (Exception) { }
            finally { _loading = false; }
            if (this != null) await LoadShipyardAsync(notice);
        }
        private sealed class AuthProxy : IAuthProvider
        {
            private readonly Func<Task<string>> _get;
            public AuthProxy(Func<Task<string>> get) { _get = get; }
            public Task<string> GetTokenAsync() => _get() ?? Task.FromResult<string>(null);
        }
    }
}
