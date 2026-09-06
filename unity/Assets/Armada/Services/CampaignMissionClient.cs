using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json.Linq;

namespace Armada.Client.Services
{
    /// <summary>Uses the existing typed scenario checks; one instance belongs to one battle.</summary>
    public sealed class CampaignMissionClient : ICampaignMissionClient
    {
        private readonly ICampaignMissionService _missions;
        private readonly FrozenCampaignScenarioClient _frozen;
        public CampaignMissionClient(ICampaignMissionService missions, IUpgradesClient upgrades = null, ICaptainProgressionClient captain = null)
        {
            _missions = missions;
            _frozen = new FrozenCampaignScenarioClient(missions, upgrades, captain);
        }

        public async Task<CampaignRunResult> ResolveAsync(int missionNumber, int seed, List<List<SimOrder>> turns)
        {
            switch (missionNumber)
            {
                case 1: { var run = await new Mission01Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 2: { var run = await new Mission02Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 3: { var run = await new Mission03Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 4: { var run = await new Mission04Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 5: { var run = await new Mission05Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 6: { var run = await new Mission06Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 7: { var run = await new Mission07Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 8: { var run = await new Mission08Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 9: { var run = await new Mission09Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 10: { var run = await new Mission10Flow(_frozen).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                default: return new CampaignRunResult { Error = "unknown_mission" };
            }
        }

        public async Task<CampaignSaveResult> CompleteAsync(int number, string code, MissionCompleteRequest request)
        {
            await _frozen.ApplyCompletionAsync(request);
            var response = await _missions.CompleteAsync(code, request);
            return new CampaignSaveResult
            {
                Success = response.Success && !response.FeatureDisabled,
                Data = response.Data,
                Error = response.ErrorReason ?? (response.FeatureDisabled ? "feature_disabled" : response.Status.ToString())
            };
        }

        private static CampaignRunResult Project(bool success, string error, object outcome) => new CampaignRunResult
        {
            Success = success && outcome != null,
            Error = error,
            Outcome = outcome == null ? null : JObject.FromObject(outcome).ToObject<CampaignOutcome>()
        };

    }
}
