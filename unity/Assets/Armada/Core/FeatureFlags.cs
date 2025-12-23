using System;
using System.Collections.Generic;
using UnityEngine;

namespace Armada.Client.Core
{
    public sealed class FeatureFlags
    {
        private readonly Dictionary<string, bool> _flags = new(StringComparer.OrdinalIgnoreCase);

        public FeatureFlags(IEnumerable<FeatureToggleSetting> defaults)
        {
            if (defaults == null) return;
            foreach (var setting in defaults)
            {
                if (!string.IsNullOrWhiteSpace(setting.key))
                {
                    _flags[setting.key] = setting.enabled;
                }
            }
        }

        public bool IsEnabled(string key, bool fallback = false)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            return _flags.TryGetValue(key, out var value) ? value : fallback;
        }

        public void Set(string key, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _flags[key] = enabled;
        }

        public void DisableFromForbidden(string key)
        {
            Debug.LogWarning($"[Flags] Feature '{key}' disabled due to backend 403.");
            Set(key, false);
        }
    }
}

