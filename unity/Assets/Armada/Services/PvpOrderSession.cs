using System.Collections.Generic;
using System.Text;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// Editable order draft for one ship. The v1 PvP order surface is
    /// maneuver (turn delta) + speed delta + optional broadside target +
    /// ammo (round/chain); ramming, boarding, and status flags are deferred.
    /// </summary>
    public sealed class PvpOrderDraft
    {
        public string ShipId { get; init; }
        public int TurnDelta { get; set; }
        public int SpeedDelta { get; set; }
        /// <summary>Null means no attack this turn (maneuver only).</summary>
        public string TargetShipId { get; set; }
        /// <summary>"round" or "chain"; only sent when a target is set.</summary>
        public string Ammo { get; set; } = "round";
    }

    /// <summary>
    /// Order-authoring model for one side of the hot-seat loop. Plain C# so
    /// tests (and any view) drive it without touching TMP or uGUI; the
    /// MonoBehaviour controller renders it and forwards button presses.
    /// </summary>
    public sealed class PvpOrderSession
    {
        // Clamp bounds mirror simOrderSchema (src/sim/types.ts).
        public const int TurnDeltaStep = 15;
        public const int TurnDeltaLimit = 90;
        public const int SpeedDeltaLimit = 2;

        private readonly List<PvpOrderDraft> _drafts = new();
        private readonly List<SimShip> _enemyShips;

        public string SideLabel { get; }
        public int ShipIndex { get; private set; }

        public PvpOrderSession(string sideLabel, IReadOnlyList<SimShip> ownShips, IReadOnlyList<SimShip> enemyShips)
        {
            SideLabel = sideLabel;
            _enemyShips = enemyShips != null ? new List<SimShip>(enemyShips) : new List<SimShip>();
            if (ownShips == null)
            {
                return;
            }

            foreach (var ship in ownShips)
            {
                if (ship?.Id != null)
                {
                    _drafts.Add(new PvpOrderDraft { ShipId = ship.Id });
                }
            }
        }

        public IReadOnlyList<PvpOrderDraft> Drafts => _drafts;

        public PvpOrderDraft CurrentDraft =>
            _drafts.Count == 0 ? null : _drafts[ShipIndex];

        public void NextShip()
        {
            if (_drafts.Count > 0)
            {
                ShipIndex = (ShipIndex + 1) % _drafts.Count;
            }
        }

        public void AdjustTurn(int direction)
        {
            var draft = CurrentDraft;
            if (draft != null)
            {
                draft.TurnDelta = Clamp(draft.TurnDelta + direction * TurnDeltaStep, -TurnDeltaLimit, TurnDeltaLimit);
            }
        }

        public void AdjustSpeed(int direction)
        {
            var draft = CurrentDraft;
            if (draft != null)
            {
                draft.SpeedDelta = Clamp(draft.SpeedDelta + direction, -SpeedDeltaLimit, SpeedDeltaLimit);
            }
        }

        /// <summary>Cycles the broadside target through the living enemy
        /// ships and back to none (maneuver only).</summary>
        public void CycleTarget()
        {
            var draft = CurrentDraft;
            if (draft == null)
            {
                return;
            }

            if (_enemyShips.Count == 0)
            {
                draft.TargetShipId = null;
                return;
            }

            var index = _enemyShips.FindIndex(ship => ship.Id == draft.TargetShipId);
            if (index < 0)
            {
                draft.TargetShipId = _enemyShips[0].Id;
            }
            else if (index + 1 < _enemyShips.Count)
            {
                draft.TargetShipId = _enemyShips[index + 1].Id;
            }
            else
            {
                draft.TargetShipId = null;
            }
        }

        public void ToggleAmmo()
        {
            var draft = CurrentDraft;
            if (draft != null)
            {
                draft.Ammo = draft.Ammo == "chain" ? "round" : "chain";
            }
        }

        /// <summary>
        /// Builds the side's order list. Every order carries the maneuver
        /// deltas (the engine applies them for any action); a set target
        /// makes it a broadside, otherwise it stays a pure maneuver. Round
        /// shot omits the ammo key so payloads stay byte-identical to the
        /// legacy shape.
        /// </summary>
        public List<SimOrder> BuildOrders()
        {
            var orders = new List<SimOrder>(_drafts.Count);
            foreach (var draft in _drafts)
            {
                orders.Add(new SimOrder
                {
                    ShipId = draft.ShipId,
                    Action = draft.TargetShipId != null ? "broadside" : "maneuver",
                    TargetShipId = draft.TargetShipId,
                    Side = draft.TargetShipId != null ? "starboard" : null,
                    TurnDelta = draft.TurnDelta,
                    SpeedDelta = draft.SpeedDelta,
                    Ammo = draft.TargetShipId != null && draft.Ammo == "chain" ? "chain" : null
                });
            }

            return orders;
        }

        /// <summary>Plain-text summary of the side's drafts for the HUD (TMP-free).</summary>
        public string Describe()
        {
            var builder = new StringBuilder();
            builder.Append("Side ").Append(SideLabel).Append(" orders:");
            for (var i = 0; i < _drafts.Count; i++)
            {
                var draft = _drafts[i];
                builder.Append('\n');
                builder.Append(i == ShipIndex ? "> " : "  ");
                builder.Append(draft.ShipId)
                    .Append(": turn ").Append(draft.TurnDelta)
                    .Append(", speed ").Append(draft.SpeedDelta >= 0 ? "+" : string.Empty).Append(draft.SpeedDelta);
                if (draft.TargetShipId != null)
                {
                    builder.Append(", fire ").Append(draft.Ammo).Append(" at ").Append(draft.TargetShipId);
                }
                else
                {
                    builder.Append(", hold fire");
                }
            }

            return builder.ToString();
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
