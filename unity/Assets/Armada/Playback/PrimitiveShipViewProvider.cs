using Armada.Client.Core;
using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// The default ship view: the pre-art primitives (player cube, enemy
    /// capsule, flat side tint) that every scene shipped with — now built
    /// behind the provider seam instead of inline in the renderer, plus the
    /// D2/W2 heading bow cue: a lighter prow block on local +z so absolute
    /// heading is readable from top-down, which the symmetric primitives
    /// never showed (docs/design/art-direction.md §3.2).
    /// </summary>
    public sealed class PrimitiveShipViewProvider : ShipViewProvider
    {
        // Primitive geometry: a unit cube's top sits 0.5 above the origin, a
        // default capsule's 1.0; bars derive their lift from these.
        private const float CubeTopClearance = 0.5f;
        private const float CapsuleTopClearance = 1f;

        public override ShipView CreateShipView(SimShip ship, Transform parent)
        {
            return BuildPrimitiveView(ship, parent);
        }

        /// <summary>
        /// The primitive build, callable without a component instance so
        /// prefab-backed providers can fall back to it per ship when a class
        /// has no prefab wired yet (art-needs.md §1).
        /// </summary>
        internal static ShipView BuildPrimitiveView(SimShip ship, Transform parent)
        {
            var isPlayer = ship.Side == "player";
            var body = GameObject.CreatePrimitive(isPlayer ? PrimitiveType.Cube : PrimitiveType.Capsule);
            body.name = $"marker-{ship.Id}";
            body.transform.SetParent(parent, worldPositionStays: false);

            // Bow cue: a lighter block ahead of the body on local +z. The
            // renderer keeps +z on the sim heading, so the prow always points
            // where the ship will move — heading was previously invisible
            // (and the yaw mapping unverifiable) on symmetric primitives.
            var prow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prow.name = "bow-cue";
            prow.transform.SetParent(body.transform, worldPositionStays: false);
            prow.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            prow.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);

            var view = body.AddComponent<ShipView>();
            view.Configure(
                body.GetComponent<Renderer>(),
                prow.GetComponent<Renderer>(),
                isPlayer ? CubeTopClearance : CapsuleTopClearance);
            return view;
        }
    }
}
