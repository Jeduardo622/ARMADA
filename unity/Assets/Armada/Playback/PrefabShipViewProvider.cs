using Armada.Client.Core;
using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// The seam's first prefab-backed provider (art-needs.md §1): looks up a
    /// prefab per <see cref="ShipClass"/> × <see cref="ShipLivery"/>,
    /// instantiates it, and hands the renderer the prefab's own
    /// <see cref="ShipView"/>. Any class/livery slot left empty — and any
    /// prefab missing its <see cref="ShipView"/> — falls back to the
    /// primitive build per ship, so a partially-arted project always renders.
    /// Prefabs are referenced by serialized field (asset-pipeline.md §5:
    /// the GUIDs are load-bearing); scene builders wire them by path.
    /// </summary>
    public sealed class PrefabShipViewProvider : ShipViewProvider
    {
        [Header("Ship prefabs (class × livery; empty slots fall back to primitives)")]
        [SerializeField] private ShipView sloopAurorian;
        [SerializeField] private ShipView sloopCrimson;
        [SerializeField] private ShipView frigateAurorian;
        [SerializeField] private ShipView frigateCrimson;
        [SerializeField] private ShipView clipperAurorian;
        [SerializeField] private ShipView clipperCrimson;
        [SerializeField] private ShipView brigAurorian;
        [SerializeField] private ShipView brigCrimson;
        [SerializeField] private ShipView capitalAurorian;
        [SerializeField] private ShipView capitalCrimson;

        public override ShipView CreateShipView(SimShip ship, Transform parent)
        {
            var prefab = Lookup(ShipClassCatalog.Classify(ship), ShipClassCatalog.LiveryFor(ship));
            if (prefab == null)
            {
                return PrimitiveShipViewProvider.BuildPrimitiveView(ship, parent);
            }

            var view = Instantiate(prefab, parent, instantiateInWorldSpace: false);
            view.gameObject.name = $"marker-{ship?.Id}";
            var scale = ShipClassCatalog.ScaleFor(ship);
            if (!Mathf.Approximately(scale, 1f))
            {
                view.ApplyUniformScale(scale);
            }

            return view;
        }

        private ShipView Lookup(ShipClass shipClass, ShipLivery livery)
        {
            var aurorian = livery == ShipLivery.Aurorian;
            switch (shipClass)
            {
                case ShipClass.Frigate:
                    return aurorian ? frigateAurorian : frigateCrimson;
                case ShipClass.Clipper:
                    return aurorian ? clipperAurorian : clipperCrimson;
                case ShipClass.Brig:
                    return aurorian ? brigAurorian : brigCrimson;
                case ShipClass.Capital:
                    return aurorian ? capitalAurorian : capitalCrimson;
                default:
                    return aurorian ? sloopAurorian : sloopCrimson;
            }
        }
    }
}
