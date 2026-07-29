using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Applies <see cref="Screen.safeArea"/> to a full-canvas RectTransform as
    /// normalized anchors, so HUD content clears notches, punch-holes, and
    /// rounded corners on mobile (GDD p.24; decision D2-B in
    /// docs/design/art-direction.md §3.3). Scene builders wrap every HUD
    /// element in one rect carrying this component. On desktop and in the
    /// Editor the safe area equals the screen, so the wrapper is a no-op and
    /// the dev harness renders exactly as before.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaInsets : MonoBehaviour
    {
        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);

        private void Awake()
        {
            Apply();
        }

        private void Update()
        {
            // Safe area changes with orientation and fold state; the compare
            // keeps the steady-state cost to one struct comparison per frame.
            if (Screen.safeArea != _applied)
            {
                Apply();
            }
        }

        private void Apply()
        {
            var safe = Screen.safeArea;
            _applied = safe;
            var (min, max) = ComputeAnchors(safe, Screen.width, Screen.height);
            var rect = (RectTransform)transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Pure anchor math, EditMode-tested: maps a pixel-space safe area to
        /// normalized anchors. Degenerate input (zero screen, empty or
        /// inverted safe area) falls back to the full screen — a bad safe
        /// area must never collapse the UI.
        /// </summary>
        public static (Vector2 min, Vector2 max) ComputeAnchors(
            Rect safeArea,
            float screenWidth,
            float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                return (Vector2.zero, Vector2.one);
            }

            var min = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            var max = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);
            min = Vector2.Max(Vector2.zero, min);
            max = Vector2.Min(Vector2.one, max);
            if (max.x <= min.x || max.y <= min.y)
            {
                return (Vector2.zero, Vector2.one);
            }

            return (min, max);
        }
    }
}
