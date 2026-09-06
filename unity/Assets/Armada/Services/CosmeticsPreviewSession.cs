using System;
using System.Collections.Generic;
using UnityEngine;

namespace Armada.Client.Services
{
    /// <summary>Local visual preview. Only a successful server response changes saved equipment.</summary>
    public sealed class CosmeticsPreviewSession
    {
        private readonly Action<Color?> _applySailTint;
        private Dictionary<string, Color?> _ownedColors;
        public string SelectedId { get; private set; }
        public string EquippedId { get; private set; }

        public CosmeticsPreviewSession(CosmeticsResponse saved, Action<Color?> applySailTint)
        {
            _applySailTint = applySailTint ?? throw new ArgumentNullException(nameof(applySailTint));
            if (!AcceptSaved(saved)) throw new ArgumentException("Invalid saved cosmetics", nameof(saved));
        }

        public bool Preview(string sailId)
        {
            if (sailId == null || !_ownedColors.TryGetValue(sailId, out var color)) return false;
            SelectedId = sailId;
            _applySailTint(color);
            return true;
        }

        public void Cancel() => Preview(EquippedId);

        // Call after GET or successful EquipAsync, never on a failed/offline request.
        public bool AcceptSaved(CosmeticsResponse saved)
        {
            if (saved == null || saved.CatalogVersion < 1 || saved.Catalog == null
                || saved.OwnedIds == null || saved.EquippedId == null) return false;
            var colors = new Dictionary<string, Color?>();
            var ids = new HashSet<string>();
            foreach (var sail in saved.Catalog)
            {
                if (sail == null || string.IsNullOrEmpty(sail.SailId) || !ids.Add(sail.SailId)) return false;
                Color? color = null;
                if (sail.SailColor != null)
                {
                    if (sail.SailColor.Length != 7 || !sail.SailColor.StartsWith("#", StringComparison.Ordinal)
                        || !ColorUtility.TryParseHtmlString(sail.SailColor, out var parsed)) return false;
                    color = parsed;
                }
                if (saved.OwnedIds.Contains(sail.SailId)) colors.Add(sail.SailId, color);
            }
            if (!colors.ContainsKey(saved.EquippedId)) return false;
            _ownedColors = colors;
            EquippedId = saved.EquippedId;
            return Preview(EquippedId);
        }
    }
}
