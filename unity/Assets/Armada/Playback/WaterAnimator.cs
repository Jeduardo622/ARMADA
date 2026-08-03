using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// Turns the painterly sea's animation on — in play mode only. The
    /// serialized material ships with _Animate = 0 so every headless
    /// capture (edit mode, no Start) renders the byte-stable frozen frame
    /// (docs/visual-regression.md determinism contract); live clients get
    /// the moving swell.
    /// </summary>
    public sealed class WaterAnimator : MonoBehaviour
    {
        private static readonly int AnimateProperty = Shader.PropertyToID("_Animate");

        [SerializeField] private Renderer waterRenderer;

        private void Start()
        {
            if (waterRenderer != null)
            {
                // Instance material: the shared asset keeps _Animate = 0 so
                // captures and other scenes stay deterministic.
                waterRenderer.material.SetFloat(AnimateProperty, 1f);
            }
        }

        /// <summary>Builder wiring (mirrors the SetReference pattern).</summary>
        public void Configure(Renderer target)
        {
            waterRenderer = target;
        }
    }
}
