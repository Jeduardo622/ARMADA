using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Armada.Client.Tests
{
    public sealed class CampaignLoadoutForwardingTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EveryMission_ForwardsOneFrozenOwnedSnapshotThroughCompletion(int number)
        {
            var source = new FakeMissions();
            var upgrades = new FakeUpgrades();
            var captain = new FakeCaptain();
            var client = new CampaignMissionClient(source, upgrades, captain);
            var first = client.ResolveAsync(number, 12, new List<List<SimOrder>>()).GetAwaiter().GetResult();
            Assert.That(first.Success, Is.True);
            Assert.That(source.ResolvedNumbers, Is.EqualTo(new[] { number }));
            AssertSnapshot(source.Requests[0].Upgrades, source.Requests[0].Loadout);
            // Mutating either server-returned DTOs or earlier request DTOs must
            // not change a battle that is already underway.
            upgrades.Data.Owned[0].Tier = 3;
            captain.Data.Captain.Level = 5;
            captain.Data.Crew.FirstMate = null;
            source.Requests[0].Upgrades.Hull = 3;
            source.Requests[0].Loadout.CaptainLevel = 5;
            var second = client.ResolveAsync(number, 12, new List<List<SimOrder>>()).GetAwaiter().GetResult();
            Assert.That(second.Success, Is.True);
            AssertSnapshot(source.Requests[1].Upgrades, source.Requests[1].Loadout);
            var complete = new MissionCompleteRequest { PlayerId = "11111111-1111-4111-8111-111111111111" };
            var saved = client.CompleteAsync(number, "proof-code", complete).GetAwaiter().GetResult();
            Assert.That(saved.Success, Is.True);
            Assert.That(source.Completion, Is.SameAs(complete));
            Assert.That(source.CompletionCode, Is.EqualTo("proof-code"));
            AssertSnapshot(complete.Upgrades, complete.Loadout);
            Assert.That(JToken.DeepEquals(JObject.FromObject(source.Requests[1].Loadout), JObject.FromObject(complete.Loadout)), Is.True);
            Assert.That(upgrades.Calls, Is.EqualTo(1));
            Assert.That(captain.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SharedDelayedFailure_RetriesThenKeepsSuccessfulSnapshot()
        {
            var source = new FakeMissions();
            var upgrades = new FakeUpgrades();
            var captain = new FakeCaptain();
            var gate = new TaskCompletionSource<ServiceResult<UpgradesResponse>>();
            upgrades.Pending = gate.Task;
            var client = new FrozenCampaignScenarioClient(source, upgrades, captain);
            var first = client.ResolveMission01Async(new Mission01ResolveRequest());
            var second = client.ResolveMission10Async(new Mission01ResolveRequest());
            Assert.That(upgrades.Calls, Is.EqualTo(1));
            Assert.That(captain.Calls, Is.Zero);
            Assert.That(source.Requests, Is.Empty);
            gate.SetResult(new ServiceResult<UpgradesResponse> { Success = false });
            for (var frame = 0; frame < 100 && (!first.IsCompleted || !second.IsCompleted); frame++) yield return null;
            Assert.That(first.IsCompleted && second.IsCompleted, Is.True, "Failed ownership fetch must settle all waiters.");
            Assert.Throws<InvalidOperationException>(() => first.GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => second.GetAwaiter().GetResult());
            Assert.That(source.Requests, Is.Empty, "No resolve may escape without its owned snapshot.");
            upgrades.Pending = null;
            var retry = client.ResolveMission01Async(new Mission01ResolveRequest());
            for (var frame = 0; frame < 100 && !retry.IsCompleted; frame++) yield return null;
            Assert.That(retry.IsCompleted, Is.True);
            Assert.That(retry.GetAwaiter().GetResult().Success, Is.True);
            var complete = new MissionCompleteRequest();
            client.ApplyCompletionAsync(complete).GetAwaiter().GetResult();
            AssertSnapshot(complete.Upgrades, complete.Loadout);
            Assert.That(upgrades.Calls, Is.EqualTo(2));
            Assert.That(captain.Calls, Is.EqualTo(1));
        }

        [Test]
        public void FailedCaptainFetch_DoesNotResolveAndCanRetryWithoutPartialSnapshot()
        {
            var source = new FakeMissions();
            var upgrades = new FakeUpgrades();
            var captain = new FakeCaptain { Fail = true };
            var client = new CampaignMissionClient(source, upgrades, captain);
            Assert.Throws<InvalidOperationException>(() => client.ResolveAsync(1, 12, new List<List<SimOrder>>()).GetAwaiter().GetResult());
            Assert.That(source.Requests, Is.Empty);
            captain.Fail = false;
            Assert.That(client.ResolveAsync(1, 12, new List<List<SimOrder>>()).GetAwaiter().GetResult().Success, Is.True);
            Assert.That(upgrades.Calls, Is.EqualTo(2));
            Assert.That(captain.Calls, Is.EqualTo(2));
        }

        private static void AssertSnapshot(SimShipUpgrades upgrades, CampaignCombatLoadout loadout)
        {
            Assert.That(upgrades.Hull, Is.EqualTo(1));
            Assert.That(upgrades.Cannon, Is.EqualTo(0));
            Assert.That(upgrades.Sail, Is.EqualTo(0));
            Assert.That(loadout.SchemaVersion, Is.EqualTo(1));
            Assert.That(loadout.CaptainLevel, Is.EqualTo(2));
            Assert.That(loadout.FirstMate, Is.EqualTo("calico_jim"));
            Assert.That(loadout.GunneryChief, Is.Null);
        }

        private sealed class FakeUpgrades : IUpgradesClient
        {
            public int Calls;
            public Task<ServiceResult<UpgradesResponse>> Pending;
            public readonly UpgradesResponse Data = new UpgradesResponse { Owned = new List<OwnedUpgrade> { new OwnedUpgrade { Component = "hull", Tier = 1 } } };
            public Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync()
            {
                Calls++;
                return Pending ?? Task.FromResult(new ServiceResult<UpgradesResponse> { Success = true, Data = Data });
            }
            public Task<ServiceResult<UpgradePurchaseResponse>> PurchaseAsync(UpgradePurchaseRequest request) => throw new NotSupportedException();
        }
        private sealed class FakeCaptain : ICaptainProgressionClient
        {
            public int Calls;
            public bool Fail;
            public readonly CaptainProgressionResponse Data = new CaptainProgressionResponse {
                Captain = new CaptainProfile { Level = 2 }, Crew = new CaptainCrew { FirstMate = "calico_jim" }
            };
            public Task<ApiResponse<CaptainProgressionResponse>> GetAsync()
            {
                Calls++;
                return Task.FromResult(Fail ? ApiResponse<CaptainProgressionResponse>.CreateFailure(HttpStatusCode.ServiceUnavailable, null, null)
                    : ApiResponse<CaptainProgressionResponse>.CreateSuccess(Data, HttpStatusCode.OK, null));
            }
        }
        private sealed class FakeMissions : ICampaignMissionService
        {
            public readonly List<int> ResolvedNumbers = new List<int>();
            public readonly List<Mission01ResolveRequest> Requests = new List<Mission01ResolveRequest>();
            public MissionCompleteRequest Completion;
            public string CompletionCode;
            private static Task<ServiceResult<T>> Success<T>(T data) => Task.FromResult(new ServiceResult<T> { Success = true, Data = data });
            private Task<ServiceResult<T>> Resolve<T>(int number, Mission01ResolveRequest request, T outcome)
            { ResolvedNumbers.Add(number); Requests.Add(request); return Success(outcome); }
            public Task<ServiceResult<Mission01StartResponse>> StartMission01Async(int seed) => Success(Mission01Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission01Outcome>> ResolveMission01Async(Mission01ResolveRequest request) => Resolve(1, request, new Mission01Outcome { Seed = request.Seed, MissionCode = Mission01Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission02StartResponse>> StartMission02Async(int seed) => Success(Mission02Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission02Outcome>> ResolveMission02Async(Mission01ResolveRequest request) => Resolve(2, request, new Mission02Outcome { Seed = request.Seed, MissionCode = Mission02Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission03StartResponse>> StartMission03Async(int seed) => Success(Mission03Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission03Outcome>> ResolveMission03Async(Mission01ResolveRequest request) => Resolve(3, request, new Mission03Outcome { Seed = request.Seed, MissionCode = Mission03Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission04StartResponse>> StartMission04Async(int seed) => Success(Mission04Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission04Outcome>> ResolveMission04Async(Mission01ResolveRequest request) => Resolve(4, request, new Mission04Outcome { Seed = request.Seed, MissionCode = Mission04Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission05StartResponse>> StartMission05Async(int seed) => Success(Mission05Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission05Outcome>> ResolveMission05Async(Mission01ResolveRequest request) => Resolve(5, request, new Mission05Outcome { Seed = request.Seed, MissionCode = Mission05Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission06StartResponse>> StartMission06Async(int seed) => Success(Mission06Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission06Outcome>> ResolveMission06Async(Mission01ResolveRequest request) => Resolve(6, request, new Mission06Outcome { Seed = request.Seed, MissionCode = Mission06Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission07StartResponse>> StartMission07Async(int seed) => Success(Mission07Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission07Outcome>> ResolveMission07Async(Mission01ResolveRequest request) => Resolve(7, request, new Mission07Outcome { Seed = request.Seed, MissionCode = Mission07Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission08StartResponse>> StartMission08Async(int seed) => Success(Mission08Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission08Outcome>> ResolveMission08Async(Mission01ResolveRequest request) => Resolve(8, request, new Mission08Outcome { Seed = request.Seed, MissionCode = Mission08Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission09StartResponse>> StartMission09Async(int seed) => Success(Mission09Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission09Outcome>> ResolveMission09Async(Mission01ResolveRequest request) => Resolve(9, request, new Mission09Outcome { Seed = request.Seed, MissionCode = Mission09Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<Mission10StartResponse>> StartMission10Async(int seed) => Success(Mission10Scenario.BuildExpectedStart(seed));
            public Task<ServiceResult<Mission10Outcome>> ResolveMission10Async(Mission01ResolveRequest request) => Resolve(10, request, new Mission10Outcome { Seed = request.Seed, MissionCode = Mission10Scenario.MissionCode, Result = "win", TurnCount = 1 });
            public Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string code, MissionCompleteRequest request)
            { Completion = request; CompletionCode = code; return Success(new MissionCompleteResponse()); }
        }
    }
}
