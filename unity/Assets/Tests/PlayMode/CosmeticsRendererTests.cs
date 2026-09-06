using System.Reflection;
using System.Collections.Generic;
using Armada.Client.Core;
using Armada.Client.Playback;
using NUnit.Framework;
using UnityEngine;

public sealed class CosmeticsRendererTests
{
    private sealed class SailProvider : ShipViewProvider
    {
        public readonly Dictionary<string, Renderer> Sails = new Dictionary<string, Renderer>();
        public override ShipView CreateShipView(SimShip ship, Transform parent)
        {
            var root = new GameObject(ship.Id);
            root.transform.SetParent(parent);
            Renderer Surface(string name)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = name;
                child.transform.SetParent(root.transform);
                return child.GetComponent<Renderer>();
            }
            var hull = Surface("Hull");
            var sail = Surface("MainSail");
            Sails[ship.Id] = sail;
            var view = root.AddComponent<ShipView>();
            view.Configure(hull, sail, 1f);
            return view;
        }
    }

    [Test]
    public void SpectatorAppliesEquipmentToExistingAndNewPlayersUsingSideNotId()
    {
        var root = new GameObject("cosmetic-spectator");
        root.SetActive(false);
        try
        {
            var provider = root.AddComponent<SailProvider>();
            var spectator = root.AddComponent<SpectatorRenderer>();
            var ships = new List<SimShip> {
                new SimShip { Id = "enemy-looking-id", Side = "player", Position = new SimVector2(), Hp = 100, Sail = 50 },
                new SimShip { Id = "player-looking-id", Side = "enemy", Position = new SimVector2(), Hp = 100, Sail = 50 }
            };
            var originalState = Newtonsoft.Json.JsonConvert.SerializeObject(ships);
            spectator.ShowBoard(ships, "preview");
            var friendlyDefault = provider.Sails[ships[0].Id].material.color;
            var enemyDefault = provider.Sails[ships[1].Id].material.color;
            var method = typeof(SpectatorRenderer).GetMethod("SetPlayerSailCosmetic");
            Assert.That(method, Is.Not.Null, "Spectator needs a persisted player sail presentation seam");
            method.Invoke(spectator, new object[] { (Color?)Color.blue });
            Assert.That(provider.Sails[ships[0].Id].material.color, Is.EqualTo(Color.blue));
            Assert.That(provider.Sails[ships[1].Id].material.color, Is.EqualTo(enemyDefault));
            spectator.ShowBoard(ships, "new board");
            Assert.That(provider.Sails[ships[0].Id].material.color, Is.EqualTo(Color.blue));
            Assert.That(provider.Sails[ships[1].Id].material.color, Is.EqualTo(enemyDefault));
            method.Invoke(spectator, new object[] { null });
            Assert.That(provider.Sails[ships[0].Id].material.color, Is.EqualTo(friendlyDefault));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(ships), Is.EqualTo(originalState));
            spectator.ShowSailPreview(ships[0]);
            Assert.That(provider.Sails[ships[0].Id].transform.parent.localScale.x, Is.EqualTo(6));
            spectator.ShowBoard(ships, "battle after preview");
            Assert.That(provider.Sails[ships[0].Id].transform.parent.localScale.x, Is.EqualTo(1));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(ships), Is.EqualTo(originalState));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SailOverridePreservesHullFlagStatusAndRestoresAuthoredColor()
    {
        var root = new GameObject("cosmetic-preview");
        try
        {
            Renderer Surface(string name)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = name;
                child.transform.SetParent(root.transform);
                return child.GetComponent<Renderer>();
            }
            var hull = Surface("Hull");
            var sail = Surface("MainSail");
            var flag = Surface("Flag");
            var rig = Surface("Rig");
            var secondSail = Surface("ForeSail");
            var view = root.AddComponent<ShipView>();
            view.Configure(hull, sail, 1f);
            typeof(ShipView).GetField("extraAccentRenderers", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, new[] { flag, rig, secondSail });
            view.SetBaseTint(Color.black, Color.yellow);
            var method = typeof(ShipView).GetMethod("SetSailCosmetic", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "ShipView needs a dedicated sail-only tint seam");
            method.Invoke(view, new object[] { (Color?)Color.blue });
            Assert.That(sail.material.color, Is.EqualTo(Color.blue));
            Assert.That(hull.material.color, Is.EqualTo(Color.black));
            Assert.That(secondSail.material.color, Is.EqualTo(Color.blue));
            Assert.That(flag.material.color, Is.EqualTo(Color.yellow));
            Assert.That(rig.material.color, Is.EqualTo(Color.yellow));
            view.SetBaseTint(Color.black, Color.yellow);
            Assert.That(sail.material.color, Is.EqualTo(Color.blue));
            view.SetStatus(false, true);
            Assert.That(sail.material.color, Is.EqualTo(Color.Lerp(Color.blue, Color.gray, .35f)));
            method.Invoke(view, new object[] { null });
            Assert.That(sail.material.color, Is.EqualTo(Color.Lerp(Color.yellow, Color.gray, .35f)));
            method.Invoke(view, new object[] { (Color?)Color.blue });
            view.SetSunk();
            Assert.That(sail.material.color, Is.Not.EqualTo(Color.blue));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
