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
        private static readonly Color EmberTint = new Color(1.00f, 0.45f, 0.10f);
        private static readonly Color SunkTint = new Color(0.10f, 0.16f, 0.24f);
        private const float SunkSubmersion = 0.35f;

        private Color _baseColor = Color.white;
        private bool _onFire;
        private bool _slowed;

        /// <summary>True once the view has sunk; flashes stop applying.</summary>
        public bool IsSunk { get; private set; }

        /// <summary>
        /// The color the hull currently rests at between flashes: the side
        /// color warmed while on fire, dimmed while slowed, or the deep-sea
        /// sunk tint. The renderer restores flashes to this.
        /// </summary>
        public Color RestingColor
        {
            get
            {
                if (IsSunk)
                {
                    return SunkTint;
                }

                var color = _baseColor;
                if (_onFire)
                {
                    color = Color.Lerp(color, EmberTint, 0.45f);
                }

                if (_slowed)
                {
                    color = Color.Lerp(color, Color.gray, 0.35f);
                }

                return color;
            }
        }

        [SerializeField] private Renderer tintRenderer;
        [SerializeField] private Renderer accentRenderer;
        [Tooltip("Additional accent surfaces (extra sails, flags on multi-renderer models); they follow the accent recolor exactly, so status dimming and sinking reach every sail (Codex P2 on PR #103).")]
        [SerializeField] private Renderer[] extraAccentRenderers;
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
            _baseColor = color;
            ApplyResting();
        }

        /// <summary>Fire/slow status presentation (modifiers.statusEffects).</summary>
        public void SetStatus(bool onFire, bool slowed)
        {
            _onFire = onFire;
            _slowed = slowed;
            ApplyResting();
        }

        /// <summary>
        /// Sink presentation (irreversible): deep-sea tint, hull settles
        /// below the waterline, accent cue dims with it.
        /// </summary>
        public void SetSunk()
        {
            if (IsSunk)
            {
                return;
            }

            IsSunk = true;
            transform.position += new Vector3(0f, -SunkSubmersion, 0f);
            ApplyResting();
        }

        private void ApplyResting()
        {
            var resting = RestingColor;
            if (tintRenderer != null)
            {
                tintRenderer.material.color = resting;
            }

            var accent = IsSunk
                ? Color.Lerp(SunkTint, Color.white, 0.15f)
                : Color.Lerp(resting, Color.white, AccentLightening);
            if (accentRenderer != null)
            {
                accentRenderer.material.color = accent;
            }

            if (extraAccentRenderers != null)
            {
                foreach (var extra in extraAccentRenderers)
                {
                    if (extra != null)
                    {
                        extra.material.color = accent;
                    }
                }
            }
        }

        /// <summary>
        /// Uniformly scales the view (the m06 reinforcement renders the
        /// capital model at 1.2 hull length instead of 2.2), keeping
        /// <see cref="TopClearance"/> honest: the reported top scales with
        /// the geometry so readout bars stay anchored to the real masthead.
        /// </summary>
        public void ApplyUniformScale(float scale)
        {
            transform.localScale *= scale;
            topClearance *= scale;
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
