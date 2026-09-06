using Armada.Client.Core;
using Armada.Client.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Armada.Client.Tests
{
    public sealed class CampaignLoadoutTests
    {
        [Test]
        public void FrozenSnapshot_DoesNotExposeMutableOwnedValues()
        {
            var upgrades = new SimShipUpgrades { Hull = 1 };
            var loadout = new CampaignCombatLoadout { CaptainLevel = 2, FirstMate = "calico_jim" };
            var snapshot = new CampaignLoadoutSnapshot(upgrades, loadout);
            upgrades.Hull = 3; loadout.CaptainLevel = 5;
            var request = new Mission01ResolveRequest();
            snapshot.Apply(request);
            Assert.That(request.Upgrades.Hull, Is.EqualTo(1));
            Assert.That(request.Loadout.CaptainLevel, Is.EqualTo(2));
            request.Upgrades.Hull = 2; request.Loadout.CaptainLevel = 4;
            var complete = new MissionCompleteRequest();
            snapshot.Apply(complete);
            Assert.That(complete.Upgrades.Hull, Is.EqualTo(1));
            Assert.That(complete.Loadout.CaptainLevel, Is.EqualTo(2));
        }

        [Test]
        public void CombatContract_UsesExplicitNullCrewAndMatchingResolveCompletion()
        {
            var snapshot = new CampaignLoadoutSnapshot(new SimShipUpgrades(), new CampaignCombatLoadout());
            var resolve = new Mission01ResolveRequest();
            var complete = new MissionCompleteRequest();
            snapshot.Apply(resolve); snapshot.Apply(complete);
            var a = JObject.Parse(JsonConvert.SerializeObject(resolve));
            var b = JObject.Parse(JsonConvert.SerializeObject(complete));
            Assert.That(JToken.DeepEquals(a["loadout"], b["loadout"]), Is.True);
            Assert.That(a["loadout"]["schemaVersion"].Value<int>(), Is.EqualTo(1));
            Assert.That(a["loadout"]["firstMate"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(a["loadout"]["gunneryChief"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(JObject.Parse(JsonConvert.SerializeObject(new Mission01ResolveRequest()))["loadout"], Is.Null);
        }
    }
}
