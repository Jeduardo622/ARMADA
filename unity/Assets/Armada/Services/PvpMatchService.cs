using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    /// <summary>
    /// Seam over the pvp_api match lifecycle routes so netplay tests can
    /// fake the backend without HTTP.
    /// </summary>
    public interface IPvpMatchClient
    {
        Task<ServiceResult<PvpMatchResponse>> CreateMatchAsync();
        Task<ServiceResult<PvpMatchResponse>> JoinMatchAsync(string code);
        Task<ServiceResult<PvpSubmitOrdersResponse>> SubmitOrdersAsync(string matchId, PvpSubmitOrdersRequest request);
        Task<ServiceResult<PvpMatchResponse>> GetMatchAsync(string matchId);
    }

    public sealed class PvpMatchService : IPvpMatchClient
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private const string FeatureKey = "pvp";

        public PvpMatchService(ApiClient client, FeatureFlags flags)
        {
            _client = client;
            _flags = flags;
        }

        public async Task<ServiceResult<PvpMatchResponse>> CreateMatchAsync()
        {
            // Creation is fully server-authoritative; the route ignores any
            // body, so none is sent.
            var resp = await _client.SendAsync<PvpMatchResponse>("/pvp/matches", UnityWebRequest.kHttpVerbPOST);
            return ServiceResult<PvpMatchResponse>.FromResponse(resp, DisableIfForbidden(resp.StatusCode));
        }

        public async Task<ServiceResult<PvpMatchResponse>> JoinMatchAsync(string code)
        {
            var resp = await _client.SendAsync<PvpMatchResponse>($"/pvp/matches/{code}/join", UnityWebRequest.kHttpVerbPOST);
            return ServiceResult<PvpMatchResponse>.FromResponse(resp, DisableIfForbidden(resp.StatusCode));
        }

        public async Task<ServiceResult<PvpSubmitOrdersResponse>> SubmitOrdersAsync(string matchId, PvpSubmitOrdersRequest request)
        {
            var resp = await _client.SendAsync<PvpSubmitOrdersResponse>($"/pvp/matches/{matchId}/orders", UnityWebRequest.kHttpVerbPOST, request);
            return ServiceResult<PvpSubmitOrdersResponse>.FromResponse(resp, DisableIfForbidden(resp.StatusCode));
        }

        public async Task<ServiceResult<PvpMatchResponse>> GetMatchAsync(string matchId)
        {
            var resp = await _client.SendAsync<PvpMatchResponse>($"/pvp/matches/{matchId}", UnityWebRequest.kHttpVerbGET);
            return ServiceResult<PvpMatchResponse>.FromResponse(resp, DisableIfForbidden(resp.StatusCode));
        }

        private bool DisableIfForbidden(HttpStatusCode status)
        {
            if (status != HttpStatusCode.Forbidden)
            {
                return false;
            }

            _flags.DisableFromForbidden(FeatureKey);
            return true;
        }
    }
}
