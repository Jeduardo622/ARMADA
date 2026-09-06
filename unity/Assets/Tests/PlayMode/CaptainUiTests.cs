using System.Collections.Generic;
using System.Linq;
using Armada.Client.Core;
using Armada.Client.Services;
using Armada.Client.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class CaptainUiTests
    {
        private static CaptainProgressionResponse Profile() => new CaptainProgressionResponse {
            Captain = new CaptainProfile { Id = "aurora_black", Name = "Aurora Black", Rarity = "Rare", Xp = 100, Level = 2, NextLevelXp = 250 },
            Training = new CaptainTraining { Sequence = 3, NextSequence = 4, ShardCost = 1, XpPerTraining = 25 },
            Crew = new CaptainCrew { FirstMate = "calico_jim", Roster = new List<CaptainCrewMember> {
                new CaptainCrewMember { Id = "calico_jim", Name = "Calico Jim", Slot = "firstMate" },
                new CaptainCrewMember { Id = "one_eyed_ella", Name = "One-Eyed Ella", Slot = "gunneryChief" }
            } }
        };
        private static Button Find(GameObject root, string name) => root.GetComponentsInChildren<Button>().Single(b => b.name == name);

        [Test]
        public void Captain_TrainingUsesDisplayedSequenceAndRequiresAffordableUncappedProfile()
        {
            var root = new GameObject("captain-ui-test");
            try {
                var view = root.AddComponent<CampaignUIController>();
                var data = Profile(); var sequence = 0;
                var inventory = new List<InventoryItem>();
                view.ShowCaptain(data, inventory, null, n => sequence = n, (_, _) => { }, () => { });
                Assert.That(Find(root, "TrainCaptain").interactable, Is.False);
                inventory.Add(new InventoryItem { ItemKey = "captain_shard", Quantity = 1 });
                view.ShowCaptain(data, inventory, null, n => sequence = n, (_, _) => { }, () => { });
                Assert.That(Find(root, "TrainCaptain").interactable, Is.True);
                data.Training.NextSequence = 5;
                Find(root, "TrainCaptain").onClick.Invoke();
                Assert.That(sequence, Is.EqualTo(4));
                data.Captain.Xp = 700; data.Captain.Level = 5; data.Captain.NextLevelXp = null;
                view.ShowCaptain(data, inventory, null, n => sequence = n, (_, _) => { }, () => { });
                Assert.That(Find(root, "TrainCaptain").interactable, Is.False);
            } finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Captain_CrewToggleUsesDisplayedOtherSlotAndDoesNotOptimisticallyMutateProfile()
        {
            var root = new GameObject("captain-ui-test");
            try {
                var view = root.AddComponent<CampaignUIController>(); var data = Profile();
                string mate = "unset", chief = "unset";
                view.ShowCaptain(data, new List<InventoryItem>(), null, _ => { }, (m, c) => { mate = m; chief = c; }, () => { });
                Find(root, "Crew_gunneryChief").onClick.Invoke();
                Assert.That(mate, Is.EqualTo("calico_jim")); Assert.That(chief, Is.EqualTo("one_eyed_ella"));
                Assert.That(data.Crew.GunneryChief, Is.Null);
                Find(root, "Crew_firstMate").onClick.Invoke();
                Assert.That(mate, Is.Null); Assert.That(chief, Is.Null);
                data.Crew.Roster.Clear();
                view.ShowCaptain(data, new List<InventoryItem>(), null, _ => { }, (_, _) => { }, () => { });
                Assert.That(Find(root, "Crew_firstMate").interactable, Is.False);
            } finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Captain_RequestShapesContainOnlySequenceOrBothNullableSlots()
        {
            Assert.That(JsonConvert.SerializeObject(new CaptainTrainingRequest { Sequence = 4 }), Is.EqualTo("{\"sequence\":4}"));
            Assert.That(JsonConvert.SerializeObject(new CaptainCrewRequest { FirstMate = null, GunneryChief = "one_eyed_ella" }),
                Is.EqualTo("{\"firstMate\":null,\"gunneryChief\":\"one_eyed_ella\"}"));
        }

        [Test]
        public void Harbor_OffersCaptainAndOnlyOffersSailsWhenWired()
        {
            var root = new GameObject("captain-ui-test");
            try {
                var view = root.AddComponent<CampaignUIController>(); var clicks = 0;
                view.ShowHarbor("", () => { }, () => { }, () => clicks++);
                Find(root, "Captain").onClick.Invoke(); Assert.That(clicks, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "Sails"), Is.False);
                view.ShowHarbor("", () => { }, () => { }, () => { }, () => clicks++);
                Find(root, "Sails").onClick.Invoke(); Assert.That(clicks, Is.EqualTo(2));
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
