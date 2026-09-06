using System.Reflection;
using System.Collections.Generic;
using Armada.Client.Playback;
using Armada.Client.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class CosmeticsTests
{
    private static CosmeticsResponse Catalog(string equipped = "default") => new CosmeticsResponse
    {
        CatalogVersion = 1, EquippedId = equipped,
        OwnedIds = new List<string> { "default", "harbor_blue" },
        Catalog = new List<CosmeticSail> {
            new CosmeticSail { SailId = "default", DisplayName = "Default", SailColor = null },
            new CosmeticSail { SailId = "harbor_blue", DisplayName = "Harbor Blue", SailColor = "#6C9FC0" }
        }
    };

    [Test]
    public void PreviewCancelAndSavedApplyKeepDistinctEquipmentState()
    {
        Color? tint = Color.red;
        var session = new CosmeticsPreviewSession(Catalog(), color => tint = color);
        Assert.That(tint, Is.Null);
        Assert.That(session.Preview("harbor_blue"), Is.True);
        Assert.That(session.SelectedId, Is.EqualTo("harbor_blue"));
        Assert.That(session.EquippedId, Is.EqualTo("default"));
        Assert.That(tint, Is.Not.Null);
        session.Cancel();
        Assert.That(tint, Is.Null);
        Assert.That(session.SelectedId, Is.EqualTo("default"));
        Assert.That(session.AcceptSaved(Catalog("harbor_blue")), Is.True);
        Assert.That(session.EquippedId, Is.EqualTo("harbor_blue"));
        session.Preview("default");
        session.Cancel();
        Assert.That(session.SelectedId, Is.EqualTo("harbor_blue"));
        Assert.That(tint, Is.Not.Null);
    }

    [Test]
    public void UnknownUnownedAndMalformedCosmeticsDoNotChangePreview()
    {
        var calls = 0;
        var session = new CosmeticsPreviewSession(Catalog(), _ => calls++);
        Assert.That(session.Preview("unknown"), Is.False);
        Assert.That(session.AcceptSaved(Catalog("unknown")), Is.False);
        var unowned = Catalog();
        unowned.OwnedIds.Remove("harbor_blue");
        Assert.That(session.AcceptSaved(unowned), Is.True);
        Assert.That(session.Preview("harbor_blue"), Is.False);
        var malformed = Catalog("harbor_blue");
        malformed.Catalog[1].SailColor = "not-a-color";
        Assert.That(session.AcceptSaved(malformed), Is.False);
        Assert.That(session.SelectedId, Is.EqualTo("default"));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void WireContractUsesExactSailOnlyFields()
    {
        var request = JObject.Parse(JsonConvert.SerializeObject(new EquipCosmeticRequest { SailId = "harbor_blue" }));
        Assert.That(request.Count, Is.EqualTo(1));
        Assert.That((string)request["sailId"], Is.EqualTo("harbor_blue"));
        var response = JsonConvert.DeserializeObject<CosmeticsResponse>(JsonConvert.SerializeObject(Catalog()));
        Assert.That(response.CatalogVersion, Is.EqualTo(1));
        Assert.That(response.Catalog[0].SailColor, Is.Null);
        Assert.That(response.Catalog[1].SailColor, Is.EqualTo("#6C9FC0"));
        Assert.That(response.OwnedIds.Count, Is.EqualTo(2));
    }

}
