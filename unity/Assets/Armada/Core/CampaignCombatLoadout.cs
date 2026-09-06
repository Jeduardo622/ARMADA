using Newtonsoft.Json;

namespace Armada.Client.Core
{
    public sealed class CampaignCombatLoadout
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        [JsonProperty("captainLevel")] public int CaptainLevel { get; set; } = 1;
        [JsonProperty("firstMate", NullValueHandling = NullValueHandling.Include)] public string FirstMate { get; set; }
        [JsonProperty("gunneryChief", NullValueHandling = NullValueHandling.Include)] public string GunneryChief { get; set; }
    }
}
