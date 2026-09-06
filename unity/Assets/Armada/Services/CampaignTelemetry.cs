using System;
using System.Collections.Generic;

namespace Armada.Client.Services
{
    /// <summary>Allowlisted, coarse, best-effort campaign events. Never supplies identifiers or raw errors.</summary>
    public sealed class CampaignTelemetry
    {
        private readonly TelemetryService _service;
        private bool _sessionStarted;
        private bool _sessionEnded;
        private int _mission;
        public CampaignTelemetry(TelemetryService service) { _service = service; }
        private void Emit(string type, Dictionary<string, object> data)
        { try { _service?.Enqueue(type, data); } catch (Exception) { } }
        private static string Allowed(string value, params string[] allowed) => Array.IndexOf(allowed, value) >= 0 ? value : "other";
        public void SessionStart(string platform, int memoryMb)
        {
            if (_sessionStarted) return;
            _sessionStarted = true;
            Emit("session_start", new Dictionary<string, object> {
                ["platform"] = Allowed(platform, "editor", "windows", "android", "ios", "mac", "linux"),
                ["memory_class"] = memoryMb <= 0 ? "unknown" : memoryMb <= 2048 ? "up_to_2gb" : memoryMb <= 4096 ? "up_to_4gb" : memoryMb <= 8192 ? "up_to_8gb" : "over_8gb"
            });
        }
        public void SessionEnd(double seconds)
        {
            if (!_sessionStarted || _sessionEnded) return;
            _sessionEnded = true;
            Emit("session_end", new Dictionary<string, object> { ["duration_minutes"] = double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : (int)Math.Min(1440, Math.Max(0, seconds / 60)) });
            try { _ = _service?.FlushNowAsync(); } catch (Exception) { }
        }
        public void MissionStart(int number)
        {
            if (number < 1 || number > 10) return;
            _mission = number;
            Emit("mission_start", new Dictionary<string, object> { ["mission"] = number });
        }
        public void MissionEnd(int number, bool won, string failReason, int turns, int stars)
        {
            if (number != _mission || _mission == 0) return;
            _mission = 0;
            Emit("mission_end", new Dictionary<string, object> {
                ["mission"] = number, ["result"] = won ? "win" : "loss",
                ["fail_reason"] = won ? "none" : Allowed(failReason, "load_failed", "connection_failed", "abandoned", "turn_limit", "player_fleet_destroyed", "timeout", "sunk", "flanked"),
                ["turns"] = Math.Min(20, Math.Max(0, turns)), ["stars"] = won ? Math.Min(3, Math.Max(1, stars)) : 0
            });
        }
        public void Performance(float fps, float frameMs)
        {
            Emit("device_perf", new Dictionary<string, object> {
                ["fps_class"] = float.IsNaN(fps) || fps <= 0 ? "unknown" : fps < 20 ? "under_20" : fps < 40 ? "20_to_39" : fps < 70 ? "40_to_69" : "70_plus",
                ["frame_class"] = float.IsNaN(frameMs) || frameMs <= 0 ? "unknown" : frameMs <= 17 ? "up_to_17ms" : frameMs <= 34 ? "up_to_34ms" : "over_34ms"
            });
        }
        public void Economy(bool source, string itemKey, int quantity, string reason)
        {
            var item = Allowed(itemKey, "gold", "timber", "ore", "captain_shard", "cosmetic_token");
            if (item == "other" || quantity <= 0) return;
            Emit(source ? "economy_source" : "economy_sink", new Dictionary<string, object> {
                ["item"] = item, ["quantity"] = Math.Min(1_000_000, quantity),
                ["reason"] = Allowed(reason, "mission_clear", "upgrade_cannon", "upgrade_sail", "upgrade_hull", "captain_training")
            });
        }
        public void SailImpression(string id) => Sail("store_impression", id);
        public void SailClick(string id) => Sail("store_click", id);
        private void Sail(string type, string id) => Emit(type, new Dictionary<string, object> { ["surface"] = "sails", ["item"] = Allowed(id, "default", "harbor_blue") });
    }
}
