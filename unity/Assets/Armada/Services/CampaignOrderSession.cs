using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class CampaignOrderDraft
    {
        public string ShipId { get; init; }
        public int TurnDelta { get; set; }
        public int SpeedDelta { get; set; }
        public string TargetShipId { get; set; }
        public string Attack { get; set; } = "broadside";
        public string Ammo { get; set; } = "round";
    }

    /// <summary>Campaign-only order editor. It authors existing server orders and never simulates damage.</summary>
    public sealed class CampaignOrderSession
    {
        private readonly List<SimShip> _own;
        private readonly List<SimShip> _enemies;
        private readonly List<CampaignOrderDraft> _drafts = new();
        public bool BoardingAllowed { get; }
        public bool ChainShotAllowed { get; }
        public int ShipIndex { get; private set; }
        public IReadOnlyList<CampaignOrderDraft> Drafts => _drafts;
        public IReadOnlyList<SimShip> Ships => _own;
        public CampaignOrderDraft Current => _drafts.Count == 0 ? null : _drafts[ShipIndex];

        public CampaignOrderSession(SimState state, bool boarding, bool chainShot,
            IReadOnlyList<CampaignOrderDraft> previous = null)
        {
            BoardingAllowed = boarding;
            ChainShotAllowed = chainShot;
            _own = (state?.Ships ?? new List<SimShip>()).Where(s => s != null && s.Hp > 0 && s.Side == "player").ToList();
            _enemies = (state?.Ships ?? new List<SimShip>()).Where(s => s != null && s.Hp > 0 && s.Side == "enemy").ToList();
            foreach (var ship in _own)
            {
                var before = previous?.FirstOrDefault(d => d.ShipId == ship.Id);
                _drafts.Add(new CampaignOrderDraft
                {
                    ShipId = ship.Id,
                    TargetShipId = _enemies.Any(s => s.Id == before?.TargetShipId) ? before.TargetShipId : null,
                    Attack = before?.Attack == "maneuver" ? "maneuver" : boarding && before?.Attack == "boarding" ? "boarding" : "broadside",
                    Ammo = chainShot && before?.Ammo == "chain" ? "chain" : "round"
                });
            }
        }

        public void NextShip() { if (_drafts.Count > 0) ShipIndex = (ShipIndex + 1) % _drafts.Count; }
        public void AdjustTurn(int direction) { if (Current != null) Current.TurnDelta = Clamp(Current.TurnDelta + Math.Sign(direction) * 15, -90, 90); }
        public void AdjustSpeed(int direction) { if (Current != null) Current.SpeedDelta = Clamp(Current.SpeedDelta + Math.Sign(direction), -2, 2); }
        public void CycleTarget()
        {
            if (Current == null) return;
            var next = _enemies.FindIndex(s => s.Id == Current.TargetShipId) + 1;
            Current.TargetShipId = next < _enemies.Count ? _enemies[next].Id : null;
        }
        public void CycleAction()
        {
            if (Current == null) return;
            Current.Attack = Current.Attack == "broadside" && BoardingAllowed ? "boarding"
                : Current.Attack == "maneuver" ? "broadside" : "maneuver";
        }
        public void ToggleAmmo()
        {
            if (ChainShotAllowed && Current != null && Current.Attack == "broadside")
                Current.Ammo = Current.Ammo == "chain" ? "round" : "chain";
        }

        public List<SimOrder> BuildOrders()
        {
            var result = new List<SimOrder>();
            foreach (var draft in _drafts)
            {
                var target = _enemies.FirstOrDefault(s => s.Id == draft.TargetShipId);
                var action = target == null || draft.Attack == "maneuver" ? "maneuver" : draft.Attack;
                var ship = _own.First(s => s.Id == draft.ShipId);
                result.Add(new SimOrder
                {
                    ShipId = draft.ShipId, Action = action,
                    TurnDelta = draft.TurnDelta, SpeedDelta = draft.SpeedDelta,
                    TargetShipId = action == "maneuver" ? null : target.Id,
                    Side = action == "broadside" ? FacingBattery(ship, target, draft.TurnDelta) : null,
                    Ammo = action == "broadside" && ChainShotAllowed && draft.Ammo == "chain" ? "chain" : null
                });
            }
            return result;
        }

        public string Describe()
        {
            var text = new StringBuilder();
            for (var i = 0; i < _drafts.Count; i++)
            {
                var d = _drafts[i];
                var ship = _own[i];
                text.Append(i == ShipIndex ? "> " : "  ").Append(Name(ship.Id));
                text.Append("  |  hull ").Append(ship.Hp).Append("  |  heading ").Append((ship.Heading + d.TurnDelta + 360) % 360).Append("°");
                text.Append("  |  speed ").Append(ship.Speed).Append(d.SpeedDelta == 0 ? "" : $" ({d.SpeedDelta:+0;-0})");
                text.Append('\n').Append("    ");
                if (d.TargetShipId == null || d.Attack == "maneuver") text.Append("Hold fire — choose Target to attack");
                else text.Append(d.Attack == "boarding" ? "Board " : d.Ammo == "chain" ? "Chain shot → " : "Round shot → ").Append(Name(d.TargetShipId));
                if (d.Attack == "boarding" && d.TargetShipId != null)
                {
                    var target = _enemies.First(s => s.Id == d.TargetShipId);
                    var dx = ship.Position.X - target.Position.X;
                    var dy = ship.Position.Y - target.Position.Y;
                    text.Append("  |  range ").Append((int)Math.Sqrt(dx * dx + dy * dy));
                    text.Append("  |  crew ").Append(ship.Crew).Append(" vs ").Append(target.Crew);
                }
                if (i + 1 < _drafts.Count) text.Append('\n');
            }
            return text.ToString();
        }

        public static string Name(string id) => string.IsNullOrEmpty(id) ? "No target"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace("player-", "").Replace("enemy-", "").Replace('-', ' '));

        private static string FacingBattery(SimShip ship, SimShip target, int turnDelta)
        {
            var bearing = Math.Atan2(target.Position.Y - ship.Position.Y, target.Position.X - ship.Position.X) * 180 / Math.PI;
            var signed = ((bearing - ship.Heading - turnDelta + 540) % 360 + 360) % 360 - 180;
            return signed >= 0 ? "port" : "starboard";
        }
        private static int Clamp(int n, int min, int max) => Math.Max(min, Math.Min(max, n));
    }
}
