using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public interface IMission01Client
    {
        Task<ServiceResult<Mission01StartResponse>> StartMission01Async(int seed);
        Task<ServiceResult<Mission01Outcome>> ResolveMission01Async(Mission01ResolveRequest request);
    }

    public interface IMission02Client
    {
        Task<ServiceResult<Mission02StartResponse>> StartMission02Async(int seed);
        Task<ServiceResult<Mission02Outcome>> ResolveMission02Async(Mission01ResolveRequest request);
    }

    public interface IMission03Client
    {
        Task<ServiceResult<Mission03StartResponse>> StartMission03Async(int seed);
        Task<ServiceResult<Mission03Outcome>> ResolveMission03Async(Mission01ResolveRequest request);
    }

    public interface IMission04Client
    {
        Task<ServiceResult<Mission04StartResponse>> StartMission04Async(int seed);
        Task<ServiceResult<Mission04Outcome>> ResolveMission04Async(Mission01ResolveRequest request);
    }

    public interface IMission05Client
    {
        Task<ServiceResult<Mission05StartResponse>> StartMission05Async(int seed);
        Task<ServiceResult<Mission05Outcome>> ResolveMission05Async(Mission01ResolveRequest request);
    }

    public interface IMission06Client
    {
        Task<ServiceResult<Mission06StartResponse>> StartMission06Async(int seed);
        Task<ServiceResult<Mission06Outcome>> ResolveMission06Async(Mission01ResolveRequest request);
    }

    public interface IMission07Client
    {
        Task<ServiceResult<Mission07StartResponse>> StartMission07Async(int seed);
        Task<ServiceResult<Mission07Outcome>> ResolveMission07Async(Mission01ResolveRequest request);
    }

    public interface IMissionCompletionClient
    {
        Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string code, MissionCompleteRequest request);
    }

    public sealed class MissionService : IMission01Client, IMission02Client, IMission03Client, IMission04Client, IMission05Client, IMission06Client, IMission07Client, IMissionCompletionClient
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

        public async Task<ServiceResult<Mission01StartResponse>> StartMission01Async(int seed)
        {
            var resp = await _client.SendAsync<Mission01StartResponse>($"/missions/{Mission01Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission01StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission01Outcome>> ResolveMission01Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission01ResolveEnvelope>($"/missions/{Mission01Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission01Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission02StartResponse>> StartMission02Async(int seed)
        {
            var resp = await _client.SendAsync<Mission02StartResponse>($"/missions/{Mission02Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission02StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission02Outcome>> ResolveMission02Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission02ResolveEnvelope>($"/missions/{Mission02Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission02Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission03StartResponse>> StartMission03Async(int seed)
        {
            var resp = await _client.SendAsync<Mission03StartResponse>($"/missions/{Mission03Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission03StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission03Outcome>> ResolveMission03Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission03ResolveEnvelope>($"/missions/{Mission03Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission03Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission04StartResponse>> StartMission04Async(int seed)
        {
            var resp = await _client.SendAsync<Mission04StartResponse>($"/missions/{Mission04Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission04StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission04Outcome>> ResolveMission04Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission04ResolveEnvelope>($"/missions/{Mission04Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission04Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission05StartResponse>> StartMission05Async(int seed)
        {
            var resp = await _client.SendAsync<Mission05StartResponse>($"/missions/{Mission05Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission05StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission05Outcome>> ResolveMission05Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission05ResolveEnvelope>($"/missions/{Mission05Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission05Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission06StartResponse>> StartMission06Async(int seed)
        {
            var resp = await _client.SendAsync<Mission06StartResponse>($"/missions/{Mission06Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission06StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission06Outcome>> ResolveMission06Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission06ResolveEnvelope>($"/missions/{Mission06Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission06Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<Mission07StartResponse>> StartMission07Async(int seed)
        {
            var resp = await _client.SendAsync<Mission07StartResponse>($"/missions/{Mission07Scenario.MissionCode}/start", UnityWebRequest.kHttpVerbPOST, new Mission01StartRequest { Seed = seed });
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<Mission07StartResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<Mission07Outcome>> ResolveMission07Async(Mission01ResolveRequest request)
        {
            var resp = await _client.SendAsync<Mission07ResolveEnvelope>($"/missions/{Mission07Scenario.MissionCode}/resolve", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<Mission07Outcome>
            {
                Data = resp.Data?.Outcome,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }

        public async Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string code, MissionCompleteRequest request)
        {
            var resp = await _client.SendAsync<MissionCompleteResponse>($"/missions/{code}/complete", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<MissionCompleteResponse>.FromResponse(resp, featureDisabled);
        }
    }
}

