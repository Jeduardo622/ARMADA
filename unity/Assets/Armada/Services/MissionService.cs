using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class MissionService
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private const string FeatureKey = "missions";

        public MissionService(ApiClient client, FeatureFlags flags)
        {
            _client = client;
            _flags = flags;
        }

        public async Task<ServiceResult<List<Mission>>> ListAsync()
        {
            var resp = await _client.SendAsync<MissionsResponse>("/missions", UnityWebRequest.kHttpVerbGET);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<List<Mission>>
            {
                Data = resp.Data?.Missions,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<MissionProgress>> CompleteAsync(string code, MissionCompleteRequest request)
        {
            var resp = await _client.SendAsync<Dictionary<string, MissionProgress>>($"/missions/{code}/complete", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            resp.Data ??= new Dictionary<string, MissionProgress>();
            resp.Data.TryGetValue("progress", out var progress);

            return new ServiceResult<MissionProgress>
            {
                Data = progress,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }
    }
}

