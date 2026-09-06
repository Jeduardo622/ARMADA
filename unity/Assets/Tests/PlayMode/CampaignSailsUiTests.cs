using System.Collections.Generic;
using System.Linq;
using Armada.Client.Services;
using Armada.Client.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class CampaignSailsUiTests
    {
        [Test]
        public void Sails_OnlyAppliesOwnedChangedSelectionAndCapturesDisplayedId()
        {
            var root = new GameObject("sails-ui-test");
            try
            {
                var view = root.AddComponent<CampaignUIController>();
                var data = new CosmeticsResponse { EquippedId = "default", OwnedIds = new List<string> { "default", "harbor_blue" },
                    Catalog = new List<CosmeticSail> { new CosmeticSail { SailId = "default", DisplayName = "Default" },
                        new CosmeticSail { SailId = "harbor_blue", DisplayName = "Harbor Blue", SailColor = "#6C9FC0" } } };
                string applied = null;
                view.ShowSails(data, "default", null, _ => { }, id => applied = id, () => { }, () => { });
                Assert.That(root.GetComponentsInChildren<Button>().Single(x => x.name == "ApplySail").interactable, Is.False);
                view.ShowSails(data, "harbor_blue", null, _ => { }, id => applied = id, () => { }, () => { });
                var button = root.GetComponentsInChildren<Button>().Single(x => x.name == "ApplySail");
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(applied, Is.EqualTo("harbor_blue"));
                view.ShowSails(data, "unowned", null, _ => { }, _ => { }, () => { }, () => { });
                Assert.That(root.GetComponentsInChildren<Button>().Single(x => x.name == "ApplySail").interactable, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
