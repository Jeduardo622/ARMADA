using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class CosmeticSail
    {
        [JsonProperty("sailId")] public string SailId { get; set; }
        [JsonProperty("displayName")] public string DisplayName { get; set; }
        [JsonProperty("sailColor")] public string SailColor { get; set; }
    }

    public sealed class CosmeticsResponse
    {
        [JsonProperty("catalogVersion")] public int CatalogVersion { get; set; }
        [JsonProperty("catalog")] public List<CosmeticSail> Catalog { get; set; }
        [JsonProperty("ownedIds")] public List<string> OwnedIds { get; set; }
        [JsonProperty("equippedId")] public string EquippedId { get; set; }
    }

    public sealed class EquipCosmeticRequest
    {
        [JsonProperty("sailId")] public string SailId { get; set; }
    }

    public sealed class CosmeticsService
    {
        private readonly ApiClient _api;
        public CosmeticsService(ApiClient api) { _api = api; }
        public Task<ApiResponse<CosmeticsResponse>> GetAsync() =>
            _api.SendAsync<CosmeticsResponse>("/players/me/cosmetics", UnityWebRequest.kHttpVerbGET);
        public Task<ApiResponse<CosmeticsResponse>> EquipAsync(string sailId) =>
            _api.SendAsync<CosmeticsResponse>("/players/me/cosmetics/equip", UnityWebRequest.kHttpVerbPOST,
                new EquipCosmeticRequest { SailId = sailId });
    }
}
