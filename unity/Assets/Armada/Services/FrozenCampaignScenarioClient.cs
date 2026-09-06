using System;
using System.Linq;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    // One decorator per battle keeps existing scenario parity checks in each typed flow.
    public sealed class FrozenCampaignScenarioClient : IMission01Client, IMission02Client, IMission03Client, IMission04Client, IMission05Client, IMission06Client, IMission07Client, IMission08Client, IMission09Client, IMission10Client
    {
        private readonly ICampaignMissionService _source;
        private readonly IUpgradesClient _upgrades;
        private readonly ICaptainProgressionClient _captain;
        private Task<CampaignLoadoutSnapshot> _snapshot;
        public FrozenCampaignScenarioClient(ICampaignMissionService source, IUpgradesClient upgrades, ICaptainProgressionClient captain)
        { _source = source; _upgrades = upgrades; _captain = captain; }
        private async Task<CampaignLoadoutSnapshot> FetchAsync()
        {
            SimShipUpgrades tiers = null;
            CampaignCombatLoadout loadout = null;
            if (_upgrades != null)
            {
                var response = await _upgrades.GetUpgradesAsync();
                if (!response.Success || response.Data?.Owned == null) throw new InvalidOperationException("upgrades_unavailable");
                tiers = new SimShipUpgrades {
                    Cannon = response.Data.Owned.FirstOrDefault(x => x.Component == "cannon")?.Tier ?? 0,
                    Sail = response.Data.Owned.FirstOrDefault(x => x.Component == "sail")?.Tier ?? 0,
                    Hull = response.Data.Owned.FirstOrDefault(x => x.Component == "hull")?.Tier ?? 0
                };
            }
            if (_captain != null)
            {
                var response = await _captain.GetAsync();
                if (!response.Success || response.Data?.Captain == null || response.Data.Crew == null)
                    throw new InvalidOperationException("captain_unavailable");
                loadout = new CampaignCombatLoadout { CaptainLevel = response.Data.Captain.Level,
                    FirstMate = response.Data.Crew.FirstMate, GunneryChief = response.Data.Crew.GunneryChief };
            }
            return new CampaignLoadoutSnapshot(tiers, loadout);
        }
        private async Task<CampaignLoadoutSnapshot> SnapshotAsync()
        {
            var pending = _snapshot ??= FetchAsync();
            try { return await pending; }
            catch
            {
                // A failed opening fetch has not created a battle loadout yet.
                // Retry may refetch; an established snapshot never refreshes mid-battle.
                if (ReferenceEquals(_snapshot, pending)) _snapshot = null;
                throw;
            }
        }
        public async Task ApplyCompletionAsync(MissionCompleteRequest request) => (await SnapshotAsync()).Apply(request);
        public Task<ServiceResult<Mission01StartResponse>> StartMission01Async(int seed) => _source.StartMission01Async(seed);
        public async Task<ServiceResult<Mission01Outcome>> ResolveMission01Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission01Async(request); }
        public Task<ServiceResult<Mission02StartResponse>> StartMission02Async(int seed) => _source.StartMission02Async(seed);
        public async Task<ServiceResult<Mission02Outcome>> ResolveMission02Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission02Async(request); }
        public Task<ServiceResult<Mission03StartResponse>> StartMission03Async(int seed) => _source.StartMission03Async(seed);
        public async Task<ServiceResult<Mission03Outcome>> ResolveMission03Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission03Async(request); }
        public Task<ServiceResult<Mission04StartResponse>> StartMission04Async(int seed) => _source.StartMission04Async(seed);
        public async Task<ServiceResult<Mission04Outcome>> ResolveMission04Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission04Async(request); }
        public Task<ServiceResult<Mission05StartResponse>> StartMission05Async(int seed) => _source.StartMission05Async(seed);
        public async Task<ServiceResult<Mission05Outcome>> ResolveMission05Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission05Async(request); }
        public Task<ServiceResult<Mission06StartResponse>> StartMission06Async(int seed) => _source.StartMission06Async(seed);
        public async Task<ServiceResult<Mission06Outcome>> ResolveMission06Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission06Async(request); }
        public Task<ServiceResult<Mission07StartResponse>> StartMission07Async(int seed) => _source.StartMission07Async(seed);
        public async Task<ServiceResult<Mission07Outcome>> ResolveMission07Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission07Async(request); }
        public Task<ServiceResult<Mission08StartResponse>> StartMission08Async(int seed) => _source.StartMission08Async(seed);
        public async Task<ServiceResult<Mission08Outcome>> ResolveMission08Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission08Async(request); }
        public Task<ServiceResult<Mission09StartResponse>> StartMission09Async(int seed) => _source.StartMission09Async(seed);
        public async Task<ServiceResult<Mission09Outcome>> ResolveMission09Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission09Async(request); }
        public Task<ServiceResult<Mission10StartResponse>> StartMission10Async(int seed) => _source.StartMission10Async(seed);
        public async Task<ServiceResult<Mission10Outcome>> ResolveMission10Async(Mission01ResolveRequest request)
        { (await SnapshotAsync()).Apply(request); return await _source.ResolveMission10Async(request); }
    }
}
