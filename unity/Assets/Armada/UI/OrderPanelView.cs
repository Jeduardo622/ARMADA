using System.Collections.Generic;
using Armada.Client.Services;
using TMPro;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Structured order-entry panel (W4 HUD IA, art-direction.md §6): one row
    /// per ship draft — cursor, ship, turn, speed, target, ammo — replacing
    /// the single Describe() text blob, so each element is an addressable
    /// slot the art pass can skin (parchment panel, per-row emphasis, ammo
    /// icons) without touching controller logic. Structure only: rows are
    /// plain TMP lines with active-row emphasis; the headline/status text
    /// stays on the scene's order label.
    /// </summary>
    public sealed class OrderPanelView : MonoBehaviour
    {
        private const float RowHeight = 44f;

        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private float rowFontSize = 26f;
        [SerializeField] private Color rowColor = new Color(0.85f, 0.88f, 0.92f);
        [SerializeField] private Color activeRowColor = Color.white;

        private readonly List<TMP_Text> _rows = new();

        /// <summary>Visible row count; test hook (TMP-free).</summary>
        public int VisibleRowCount
        {
            get
            {
                var count = 0;
                foreach (var row in _rows)
                {
                    if (row != null && row.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Caption of a row, or null when hidden; test hook (TMP-free).</summary>
        public string RowCaption(int index)
        {
            return index >= 0 && index < _rows.Count && _rows[index].gameObject.activeSelf
                ? _rows[index].text
                : null;
        }

        /// <summary>Renders one structured row per draft; cursor marks the active ship.</summary>
        public void Render(PvpOrderSession session)
        {
            if (session == null)
            {
                Clear();
                return;
            }

            EnsureRows(session.Drafts.Count);
            for (var i = 0; i < session.Drafts.Count; i++)
            {
                var draft = session.Drafts[i];
                var active = i == session.ShipIndex;
                var row = _rows[i];
                row.text =
                    $"{(active ? "▶ " : "   ")}{draft.ShipId}   " +
                    $"turn {draft.TurnDelta:+0;-0;0}°   " +
                    $"speed {draft.SpeedDelta:+0;-0;0}   " +
                    $"{(draft.TargetShipId != null ? $"fire {draft.Ammo} at {draft.TargetShipId}" : "hold fire")}";
                row.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                row.color = active ? activeRowColor : rowColor;
                row.gameObject.SetActive(true);
            }

            for (var i = session.Drafts.Count; i < _rows.Count; i++)
            {
                _rows[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Hides every row (non-entry phases).</summary>
        public void Clear()
        {
            foreach (var row in _rows)
            {
                row.gameObject.SetActive(false);
            }
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var rowObject = new GameObject($"order-row-{_rows.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
                rowObject.transform.SetParent(rowContainer != null ? rowContainer : (RectTransform)transform, worldPositionStays: false);
                var rect = rowObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -_rows.Count * RowHeight);
                rect.sizeDelta = new Vector2(0f, RowHeight);
                var text = rowObject.GetComponent<TextMeshProUGUI>();
                text.fontSize = rowFontSize;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                _rows.Add(text);
            }
        }
    }
}
