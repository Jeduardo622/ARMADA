using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// The contract a ship's visual representation satisfies (W2 view
    /// abstraction, docs/design/art-direction.md §7): the renderer moves,
    /// rotates, tints, and measures the view through this component and never
    /// touches its internals — so a prefab with a real model can replace the
    /// default primitives without a renderer change. Requirements every view
    /// must meet:
    ///  - the root's local +z is the bow (the renderer yaws the root so +z
    ///    tracks the sim heading), and the silhouette must be directional
    ///    from top-down at gameplay zoom;
    ///  - <see cref="TintRenderer"/> is the surface side tinting and
    ///    broadside/ram flashes recolor; an optional accent renderer (the
    ///    primitive bow cue, sails on a real model) follows the base tint at
    ///    a lighter shade and is never flashed;
    ///  - <see cref="TopClearance"/> is the height of the view's highest
    ///    point above its origin, in world units — readout bars float above
    ///    it, so it must be honest for any model.
    /// </summary>
    public sealed class ShipView : MonoBehaviour
    {
        private const float AccentLightening = 0.4f;

        [SerializeField] private Renderer tintRenderer;
        [SerializeField] private Renderer accentRenderer;
        [Tooltip("Height of the view's highest point above its origin, in world units; readout bars derive their lift from it.")]
        [SerializeField] private float topClearance = 1f;

        public Renderer TintRenderer => tintRenderer;

        public float TopClearance => topClearance;

        /// <summary>
        /// Applies the side color: the tint surface takes it directly, the
        /// accent (bow cue) a lighter shade. Flashes recolor
        /// <see cref="TintRenderer"/> only, so the accent stays a stable
        /// heading cue mid-flash.
        /// </summary>
        public void SetBaseTint(Color color)
        {
            if (tintRenderer != null)
            {
                tintRenderer.material.color = color;
            }

            if (accentRenderer != null)
            {
                accentRenderer.material.color = Color.Lerp(color, Color.white, AccentLightening);
            }
        }

        /// <summary>Wires the view; called by providers when building views in code.</summary>
        public void Configure(Renderer tint, Renderer accent, float clearance)
        {
            tintRenderer = tint;
            accentRenderer = accent;
            topClearance = clearance;
        }
    }
}
