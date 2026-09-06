using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class CaptainProgressionResponse
    {
        public CaptainProfile Captain { get; set; }
        public CaptainTraining Training { get; set; }
        public CaptainCrew Crew { get; set; }
    }
    public sealed class CaptainProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Rarity { get; set; }
        public int Xp { get; set; }
        public int Level { get; set; }
        public int? NextLevelXp { get; set; }
    }
    public sealed class CaptainTraining
    {
        public int Sequence { get; set; }
        public int NextSequence { get; set; }
        public int ShardCost { get; set; }
        public int XpPerTraining { get; set; }
    }
    public sealed class CaptainCrew
    {
        public string FirstMate { get; set; }
        public string GunneryChief { get; set; }
        public List<CaptainCrewMember> Roster { get; set; }
    }
    public sealed class CaptainCrewMember
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Slot { get; set; }
    }
    public sealed class CaptainTrainingRequest
    {
        [JsonProperty("sequence")] public int Sequence { get; set; }
    }
    public sealed class CaptainCrewRequest
    {
        [JsonProperty("firstMate", NullValueHandling = NullValueHandling.Include)] public string FirstMate { get; set; }
        [JsonProperty("gunneryChief", NullValueHandling = NullValueHandling.Include)] public string GunneryChief { get; set; }
    }
    public interface ICaptainProgressionClient
    {
        Task<ApiResponse<CaptainProgressionResponse>> GetAsync();
    }
    public sealed class CaptainProgressionService : ICaptainProgressionClient
    {
        private readonly ApiClient _api;
        public CaptainProgressionService(ApiClient api) { _api = api; }
        public Task<ApiResponse<CaptainProgressionResponse>> GetAsync() =>
            _api.SendAsync<CaptainProgressionResponse>("/players/me/progression", UnityWebRequest.kHttpVerbGET);
        public Task<ApiResponse<CaptainProgressionResponse>> TrainAsync(int sequence) =>
            _api.SendAsync<CaptainProgressionResponse>("/players/me/progression/train", UnityWebRequest.kHttpVerbPOST,
                new CaptainTrainingRequest { Sequence = sequence });
        public Task<ApiResponse<CaptainProgressionResponse>> AssignCrewAsync(string firstMate, string gunneryChief) =>
            _api.SendAsync<CaptainProgressionResponse>("/players/me/progression/crew", UnityWebRequest.kHttpVerbPOST,
                new CaptainCrewRequest { FirstMate = firstMate, GunneryChief = gunneryChief });
    }
}
