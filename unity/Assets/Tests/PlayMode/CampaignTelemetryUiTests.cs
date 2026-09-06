using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Armada.Client.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class CampaignTelemetryUiTests
    {
        [Test]
        public void SaveRetry_RecordsOneCompletedMissionAndOnlyConfirmedRewards()
        {
            var root = new GameObject("campaign-telemetry-test");
            try
            {
                var queue = new TelemetryQueue(new JsonSerializerSettings(), 10000);
                using var service = new TelemetryService((ApiClient)null, queue, 5, 25, 10000, "qa");
                var telemetry = new CampaignTelemetry(service);
                var client = new Mission();
                var view = root.AddComponent<CampaignUIController>();
                var play = root.AddComponent<CampaignPlayController>();
                var battle = new CampaignBattleSession(CampaignCatalog.All[0], client);
                play.Compose(view, null, battle, () => "qa", () => { }, _ => { }, telemetry);
                play.BeginMission();
                play.OnConfirm();
                Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.SaveFailed));
                Assert.That(queue.DequeueBatch(25).Select(x => x.Type), Is.EqualTo(new[] { "mission_start" }));
                play.OnRetrySave();
                Assert.That(battle.Phase, Is.EqualTo(CampaignPhase.Saved));
                play.OnRetrySave();
                Assert.That(queue.DequeueBatch(25).Select(x => x.Type), Is.EqualTo(new[] { "mission_end", "economy_source" }));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Mission : ICampaignMissionClient
        {
            private int _saves;
            public Task<CampaignRunResult> ResolveAsync(int number, int seed, List<List<SimOrder>> turns)
            {
                var board = CampaignCatalog.All[0].StartState();
                return Task.FromResult(new CampaignRunResult { Success = true, Outcome = new CampaignOutcome {
                    MissionCode = CampaignCatalog.All[0].Code, Seed = seed, Result = "win", TurnCount = 1,
                    BonusObjectives = new Dictionary<string, bool>(), Turns = new List<Mission01TurnRecord> {
                        new Mission01TurnRecord { Turn = 1, StartState = board, NextState = board, Events = new List<SimEvent>() }
                    } } });
            }
            public Task<CampaignSaveResult> CompleteAsync(int number, string code, MissionCompleteRequest request) =>
                Task.FromResult(new CampaignSaveResult { Success = ++_saves > 1, Data = new MissionCompleteResponse {
                    RewardsGranted = new List<RewardGrant> { new RewardGrant { ItemKey = "gold", Quantity = 100 } }
                } });
        }
    }
}
