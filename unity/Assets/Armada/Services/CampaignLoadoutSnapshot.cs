using Armada.Client.Core;
using Newtonsoft.Json;

namespace Armada.Client.Services
{
    /// <summary>Never shares mutable request values between resolve, undo, or save.</summary>
    public sealed class CampaignLoadoutSnapshot
    {
        private readonly SimShipUpgrades _upgrades;
        private readonly CampaignCombatLoadout _loadout;
        public CampaignLoadoutSnapshot(SimShipUpgrades upgrades, CampaignCombatLoadout loadout)
        { _upgrades = Copy(upgrades); _loadout = Copy(loadout); }
        public void Apply(Mission01ResolveRequest request)
        { request.Upgrades = Copy(_upgrades); request.Loadout = Copy(_loadout); }
        public void Apply(MissionCompleteRequest request)
        { request.Upgrades = Copy(_upgrades); request.Loadout = Copy(_loadout); }
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }
}
