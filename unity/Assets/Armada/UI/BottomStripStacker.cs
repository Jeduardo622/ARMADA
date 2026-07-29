using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Stacks bottom-anchored HUD strips upward from the screen edge using
    /// their live layout heights, so wrapping button grids can never overlap
    /// each other at any aspect ratio (Codex P2 on PR #84: fixed offsets
    /// assume single-row strips, but grids wrap to different row counts as
    /// the viewport narrows). Strips are ordered bottom-first; each one sits
    /// `spacing` above the previous strip's top. Runs in LateUpdate so the
    /// ContentSizeFitter/GridLayoutGroup pass has already sized the rects.
    /// </summary>
    public sealed class BottomStripStacker : MonoBehaviour
    {
        [Tooltip("Strips in stacking order, bottom-most first. Each must be bottom-anchored with pivot y = 0.")]
        [SerializeField] private RectTransform[] strips;
        [Tooltip("Offset of the lowest strip from the bottom edge, in reference units.")]
        [SerializeField] private float edgeOffset = 24f;
        [Tooltip("Vertical gap between strips, in reference units.")]
        [SerializeField] private float spacing = 12f;

        private void LateUpdate()
        {
            Restack();
        }

        /// <summary>
        /// Applies the stacking pass immediately. Public so headless capture
        /// tooling can drive it outside play mode, where LateUpdate never
        /// runs (the HUD aspect matrix must show the stacked layout).
        /// </summary>
        public void Restack()
        {
            if (strips == null)
            {
                return;
            }

            var y = edgeOffset;
            foreach (var strip in strips)
            {
                if (strip == null)
                {
                    continue;
                }

                var position = strip.anchoredPosition;
                if (!Mathf.Approximately(position.y, y))
                {
                    strip.anchoredPosition = new Vector2(position.x, y);
                }

                y = NextOffset(y, strip.rect.height, spacing);
            }
        }

        /// <summary>Pure accumulation step, EditMode-tested.</summary>
        public static float NextOffset(float currentOffset, float stripHeight, float spacing)
        {
            return currentOffset + Mathf.Max(0f, stripHeight) + spacing;
        }
    }
}
