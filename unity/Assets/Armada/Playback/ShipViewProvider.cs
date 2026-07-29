using Armada.Client.Core;
using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// Factory seam for ship visuals (W2 view abstraction): the renderer asks
    /// its provider for a <see cref="ShipView"/> per ship and never calls
    /// CreatePrimitive itself. The default is
    /// <see cref="PrimitiveShipViewProvider"/>; an art-backed provider
    /// (prefab per ship class/faction) replaces it by living on the same
    /// GameObject as the renderer, or via the renderer's serialized
    /// provider field — no renderer change required.
    /// </summary>
    public abstract class ShipViewProvider : MonoBehaviour
    {
        /// <summary>
        /// Builds the view for a ship at spawn. The renderer owns position
        /// and rotation afterwards; the provider owns shape, materials, and
        /// an honest <see cref="ShipView.TopClearance"/>.
        /// </summary>
        public abstract ShipView CreateShipView(SimShip ship, Transform parent);
    }
}
