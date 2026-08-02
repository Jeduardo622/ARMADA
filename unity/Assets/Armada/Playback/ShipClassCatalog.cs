using Armada.Client.Core;

namespace Armada.Client.Playback
{
    /// <summary>
    /// Visual ship classes (docs/design/art-needs.md §3 P1). The wire
    /// <see cref="SimShip"/> carries no class field; class is a presentation
    /// concern derived from the stable ship-id vocabulary the missions and
    /// PvP scenarios use (player-sloop-a, enemy-frigate, enemy-flagship...).
    /// </summary>
    public enum ShipClass
    {
        Sloop,
        Frigate,
        Clipper,
        Brig,
        Capital
    }

    /// <summary>Faction livery (art-needs.md §3): side A/player sails under
    /// the Aurorian Empire, side B/enemy under the Crimson Republic.</summary>
    public enum ShipLivery
    {
        Aurorian,
        Crimson
    }

    /// <summary>
    /// Maps sim ships to visual class/livery for prefab lookup. Unknown ids
    /// fall back to <see cref="ShipClass.Sloop"/> — every ship must render,
    /// so the catalog never fails, it defaults.
    /// </summary>
    public static class ShipClassCatalog
    {
        // The m06 reinforcement is the capital model at hull length 1.2
        // instead of 2.2 (art-needs.md: "scaled variant acceptable").
        public const float ReinforcementScale = 1.2f / 2.2f;

        public static ShipClass Classify(SimShip ship)
        {
            var id = ship?.Id ?? string.Empty;
            if (id.Contains("frigate") || id.Contains("escort"))
            {
                return ShipClass.Frigate;
            }

            if (id.Contains("clipper"))
            {
                return ShipClass.Clipper;
            }

            if (id.Contains("brig"))
            {
                return ShipClass.Brig;
            }

            if (id.Contains("flagship") || id.Contains("dreadnought") || id.Contains("reinforcement"))
            {
                return ShipClass.Capital;
            }

            // sloop, aggressor, kite, corvette, and anything future-unknown.
            return ShipClass.Sloop;
        }

        public static ShipLivery LiveryFor(SimShip ship)
        {
            return ship?.Side == "player" ? ShipLivery.Aurorian : ShipLivery.Crimson;
        }

        /// <summary>Uniform world-scale multiplier applied to the class
        /// prefab; 1 for everything except the scaled capital variant.</summary>
        public static float ScaleFor(SimShip ship)
        {
            var id = ship?.Id ?? string.Empty;
            return id.Contains("reinforcement") ? ReinforcementScale : 1f;
        }
    }
}
