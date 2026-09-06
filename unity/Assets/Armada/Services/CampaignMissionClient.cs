using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json.Linq;

namespace Armada.Client.Services
{
    /// <summary>Uses the existing typed scenario checks; one instance belongs to one battle.</summary>
    public sealed class CampaignMissionClient : ICampaignMissionClient
    {
        private readonly MissionService _missions;
        private readonly Mission07Flow _seven;
        public CampaignMissionClient(MissionService missions, IUpgradesClient upgrades = null)
        {
            _missions = missions;
            _seven = new Mission07Flow(missions, upgradesClient: upgrades == null ? null : new FrozenUpgrades(upgrades), completionClient: missions);
        }

        public async Task<CampaignRunResult> ResolveAsync(int missionNumber, int seed, List<List<SimOrder>> turns)
        {
            switch (missionNumber)
            {
                case 1: { var run = await new Mission01Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 2: { var run = await new Mission02Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 3: { var run = await new Mission03Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 4: { var run = await new Mission04Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 5: { var run = await new Mission05Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 6: { var run = await new Mission06Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 7: { var run = await _seven.RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 8: { var run = await new Mission08Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 9: { var run = await new Mission09Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                case 10: { var run = await new Mission10Flow(_missions).RunAsync(seed, turns); return Project(run.Success, run.FailureReason, run.Outcome); }
                default: return new CampaignRunResult { Error = "unknown_mission" };
            }
        }

        public async Task<CampaignSaveResult> CompleteAsync(int number, string code, MissionCompleteRequest request)
        {
            // Mission07 owns the frozen upgrade tiers as well as seed/orders.
            var response = number == 7
                ? await _seven.CompleteAsync(request.PlayerId, request.Result, request.BestScore)
                : await _missions.CompleteAsync(code, request);
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

        private sealed class FrozenUpgrades : IUpgradesClient
        {
            private readonly IUpgradesClient _source;
            private Task<ServiceResult<UpgradesResponse>> _snapshot;
            public FrozenUpgrades(IUpgradesClient source) { _source = source; }
            public Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync() => _snapshot ??= _source.GetUpgradesAsync();
            public Task<ServiceResult<UpgradePurchaseResponse>> PurchaseAsync(UpgradePurchaseRequest request)
                => Task.FromResult(new ServiceResult<UpgradePurchaseResponse> { ErrorReason = "battle_in_progress" });
        }
    }
}
