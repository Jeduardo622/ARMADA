using System.Collections.Generic;
using System.Linq;
using Armada.Client.Core;
using Armada.Client.UI;
using Armada.Client.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class CampaignUiTests
    {
        [Test]
        public void Shipyard_OnlyOffersAffordableDisplayedTier()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                var offered = 0;
                var catalog = new UpgradesResponse
                {
                    Owned = new List<OwnedUpgrade> { new OwnedUpgrade { Component = "hull", Tier = 0 } },
                    Catalog = new List<UpgradeCatalogEntry> { new UpgradeCatalogEntry { Component = "hull", Tiers = new List<UpgradeCatalogTier>
                    { new UpgradeCatalogTier { Tier = 1, Costs = new List<UpgradeCost> { new UpgradeCost { ItemKey = "gold", Quantity = 100 } } } } } }
                };
                view.ShowShipyard(catalog, new List<InventoryItem>(), null, (_, tier) => offered = tier, () => { });
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Purchase_hull").interactable, Is.False);
                view.ShowShipyard(catalog, new List<InventoryItem> { new InventoryItem { ItemKey = "gold", Quantity = 100 } }, null, (_, tier) => offered = tier, () => { });
                var purchase = root.GetComponentsInChildren<Button>().Single(b => b.name == "Purchase_hull");
                Assert.That(purchase.interactable, Is.True);
                purchase.onClick.Invoke();
                Assert.That(offered, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Battle_DisablesOrdersDuringPlaybackAndWiresEditableControls()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                var clicks = 0;
                var actions = new CampaignBattleActions { Confirm = () => clicks++ };
                var mission = CampaignCatalog.All[0];
                view.ShowBattle(mission, 1, "Sloop", "Wind", false, false, actions);
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Confirm").interactable, Is.False);
                view.ShowBattle(mission, 1, "Sloop", "Wind", true, false, actions);
                var confirm = root.GetComponentsInChildren<Button>().Single(b => b.name == "Confirm");
                Assert.That(confirm.interactable, Is.True);
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Undo").interactable, Is.False);
                // iPhone 8 landscape: 750 physical pixels at 2x. The documented minimum is 44 points.
                foreach (var button in root.GetComponentsInChildren<Button>())
                {
                    var rect = button.GetComponent<RectTransform>();
                    Assert.That((rect.anchorMax.y - rect.anchorMin.y) * 750 / 2, Is.GreaterThanOrEqualTo(44), button.name);
                }
                confirm.onClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnsavedVictory_OffersSaveRetryAndDoesNotOfferNextMission()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                var retries = 0;
                view.ShowResults(CampaignCatalog.All[0], CampaignPhase.SaveFailed, 3,
                    "Your victory has not been saved.", () => retries++, () => { }, () => { });
                var buttons = root.GetComponentsInChildren<Button>();
                Assert.That(buttons.Any(b => b.name == "Next"), Is.False);
                Assert.That(buttons.Any(b => b.name == "Harbor"), Is.True, "A permanently rejected save must not trap the player.");
                buttons.Single(b => b.name == "RetrySave").onClick.Invoke();
                Assert.That(retries, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Chart_OpensFirstMissionAndKeepsLaterMissionsLocked()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                view.Initialize(null);
                var selected = 0;
                view.ShowChart(new HashSet<string>(), number => selected = number, () => { });
                var buttons = root.GetComponentsInChildren<Button>();
                Assert.That(buttons.Length, Is.EqualTo(11));
                var first = buttons.Single(b => b.name == "Mission01");
                var second = buttons.Single(b => b.name == "Mission02");
                Assert.That(first.interactable, Is.True);
                Assert.That(second.interactable, Is.False);
                first.onClick.Invoke();
                Assert.That(selected, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Chart_UnlocksNextMissionFromSavedCompletion()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                view.Initialize(null);
                view.ShowChart(new HashSet<string> { Mission01Scenario.MissionCode }, _ => { }, () => { });
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Mission02").interactable, Is.True);
                Assert.That(root.GetComponentsInChildren<Button>().Single(b => b.name == "Mission03").interactable, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Briefing_WiresLaunchAndReturnActions()
        {
            var root = new GameObject("campaign-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                view.Initialize(null);
                var launched = false;
                var returned = false;
                view.ShowBriefing(CampaignCatalog.All[0], () => launched = true, () => returned = true);
                var buttons = root.GetComponentsInChildren<Button>();
                buttons.Single(b => b.name == "Launch").onClick.Invoke();
                buttons.Single(b => b.name == "Back").onClick.Invoke();
                Assert.That(launched, Is.True);
                Assert.That(returned, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
