using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    ///
    /// W4 portrait restructure (art-direction.md §7, remaining slice): on
    /// portrait aspects the authored 190×140 cells fit only two columns of
    /// the ~607-unit reference width, so the wrapped strips consumed almost
    /// the whole safe area and buried the narration zone. When the HUD area
    /// is taller than wide the stacker swaps every strip grid to the smaller
    /// portrait cell size (three columns → roughly half the rows) and
    /// centers the rows; authored values are restored on wide aspects.
    /// Touch floor stays honest: the scaler matches height, and the minimum
    /// supported device (iPhone 8) in portrait matches 1334 px against the
    /// 1080 reference (scale ~1.235), so 110-unit cells render at ~136 px ≈
    /// 68 pt on its 2x display — above the 44 pt floor.
    ///
    /// Risers extend the same guarantee to the order-panel text and order
    /// rows (zone-map items 4-5, which sit directly above the button
    /// strips): they are restacked above the top strip, so wrapped button
    /// rows can never sit under the order panel on narrow aspects.
    /// </summary>
    public sealed class BottomStripStacker : MonoBehaviour
    {
        [Tooltip("Strips in stacking order, bottom-most first. Each must be bottom-anchored with pivot y = 0.")]
        [SerializeField] private RectTransform[] strips;
        [Tooltip("Offset of the lowest strip from the bottom edge, in reference units.")]
        [SerializeField] private float edgeOffset = 24f;
        [Tooltip("Vertical gap between strips, in reference units.")]
        [SerializeField] private float spacing = 12f;

        [Header("Portrait restructure (W4)")]
        [Tooltip("Grid cell size applied while the HUD area is taller than wide; three columns fit the portrait reference width where the authored cells fit two. Authored sizes come back on wide aspects.")]
        [SerializeField] private Vector2 portraitCellSize = new Vector2(150f, 110f);
        [Tooltip("Bottom-anchored rects (order text line, order rows) stacked above the top strip, bottom-most first, so wrapped button rows can never overlap them. Same pivot/anchor contract as strips.")]
        [SerializeField] private RectTransform[] risers;
        [Tooltip("Riser height while portrait: the authored 200-unit rects on top of three wrapped portrait strips overflow the 1080 canvas (Codex P2 on PR #101); 140 fits three 44-unit order rows and keeps the netplay stack inside the viewport. Authored heights come back on wide aspects.")]
        [SerializeField] private float portraitRiserHeight = 140f;

        private readonly Dictionary<GridLayoutGroup, (Vector2 cellSize, TextAnchor alignment)> _authored = new();
        private readonly Dictionary<RectTransform, float> _authoredRiserHeights = new();

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

            ApplyAspectCellSizes();

            var y = edgeOffset;
            foreach (var strip in strips)
            {
                y = Place(strip, y);
            }

            if (risers != null)
            {
                foreach (var riser in risers)
                {
                    y = Place(riser, y);
                }
            }
        }

        // Swaps every strip grid between its authored (landscape) cell size
        // and the portrait size as the aspect crosses square, forcing an
        // immediate layout rebuild on change so the stacking pass below reads
        // the re-wrapped heights, not the stale ones (the capture harness
        // calls Restack exactly once per aspect).
        private void ApplyAspectCellSizes()
        {
            var container = Container();
            if (container == null)
            {
                return;
            }

            var portrait = IsPortrait(container.rect.width, container.rect.height);
            ApplyRiserHeights(portrait);
            foreach (var strip in strips)
            {
                if (strip == null)
                {
                    continue;
                }

                var grid = strip.GetComponent<GridLayoutGroup>();
                if (grid == null)
                {
                    continue;
                }

                if (!_authored.TryGetValue(grid, out var authored))
                {
                    authored = (grid.cellSize, grid.childAlignment);
                    _authored[grid] = authored;
                }

                var cellSize = portrait ? portraitCellSize : authored.cellSize;
                var alignment = portrait ? CenteredInRow(authored.alignment) : authored.alignment;
                if (grid.cellSize != cellSize || grid.childAlignment != alignment)
                {
                    grid.cellSize = cellSize;
                    grid.childAlignment = alignment;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(strip);
                }
            }
        }

        // Compacts the riser rects while portrait so the full stack (three
        // wrapped strips + order text + order rows) stays inside the
        // reference canvas; authored heights restore on wide aspects.
        private void ApplyRiserHeights(bool portrait)
        {
            if (risers == null)
            {
                return;
            }

            foreach (var riser in risers)
            {
                if (riser == null)
                {
                    continue;
                }

                if (!_authoredRiserHeights.TryGetValue(riser, out var authored))
                {
                    authored = riser.sizeDelta.y;
                    _authoredRiserHeights[riser] = authored;
                }

                var height = portrait ? Mathf.Min(portraitRiserHeight, authored) : authored;
                if (!Mathf.Approximately(riser.sizeDelta.y, height))
                {
                    riser.sizeDelta = new Vector2(riser.sizeDelta.x, height);
                }
            }
        }

        // The strips' shared parent (the safe-area wrapper) is the HUD area
        // whose aspect decides the layout: in the capture harness the canvas
        // is sized per matrix aspect while Screen keeps the editor's own
        // size, so Screen must never be consulted here.
        private RectTransform Container()
        {
            if (strips == null)
            {
                return null;
            }

            foreach (var strip in strips)
            {
                if (strip != null)
                {
                    return strip.parent as RectTransform;
                }
            }

            return null;
        }

        private float Place(RectTransform rect, float y)
        {
            if (rect == null)
            {
                return y;
            }

            var position = rect.anchoredPosition;
            if (!Mathf.Approximately(position.y, y))
            {
                rect.anchoredPosition = new Vector2(position.x, y);
            }

            return NextOffset(y, rect.rect.height, spacing);
        }

        /// <summary>Pure accumulation step, EditMode-tested.</summary>
        public static float NextOffset(float currentOffset, float stripHeight, float spacing)
        {
            return currentOffset + Mathf.Max(0f, stripHeight) + spacing;
        }

        /// <summary>Pure aspect predicate, test hook.</summary>
        public static bool IsPortrait(float width, float height)
        {
            return width < height;
        }

        /// <summary>
        /// Pure alignment mapping, test hook: keeps the authored vertical row
        /// (upper/middle/lower) but centers horizontally, so portrait strips
        /// lose the dead right margin the left-aligned landscape grid leaves.
        /// </summary>
        public static TextAnchor CenteredInRow(TextAnchor authored)
        {
            var row = (int)authored / 3;
            return (TextAnchor)(row * 3 + 1);
        }
    }
}
