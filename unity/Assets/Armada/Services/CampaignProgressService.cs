using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class CampaignProgressEntry
    {
        public string MissionCode { get; set; }
        public string Status { get; set; }
        public int? VerifiedStars { get; set; }
    }
    public sealed class CampaignProgressResponse
    {
        public List<CampaignProgressEntry> Progress { get; set; }
    }
    public sealed class CampaignProgressService
    {
        private readonly ApiClient _api;
        public CampaignProgressService(ApiClient api) { _api = api; }
        public Task<ApiResponse<CampaignProgressResponse>> GetAsync() =>
            _api.SendAsync<CampaignProgressResponse>("/players/me/progress", UnityWebRequest.kHttpVerbGET);
    }
}
