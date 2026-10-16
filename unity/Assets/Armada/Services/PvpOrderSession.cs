using System.Collections.Generic;
using System.Text;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// Editable order draft for one ship. The v1 PvP order surface is
    /// maneuver (turn delta) + speed delta + optional broadside target +
    /// ammo (round/chain). Mission play may also enable boarding, which
    /// turns the target into a boarding attempt instead of a broadside.
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
        /// <summary>True when the set target is boarded rather than fired
        /// on; only reachable when the session allows boarding.</summary>
        public bool Board { get; set; }
    }

    /// <summary>
    /// Order-authoring model for one side of a turn. Plain C# so tests (and
    /// any view) drive it without touching TMP or uGUI; the MonoBehaviour
    /// controller renders it and forwards button presses.
    ///
    /// Shared by the PvP hot-seat loop (a session per side per turn) and the
    /// playable Mission 10 loop (one session per turn for the player's
    /// sloops): both author the same v1 order surface, and mission 10's
    /// ammo toggle is the same chain/round selection. The campaign's
    /// mission-agnostic loop gates the optional surfaces per mission: ammo
    /// only where the mission runs chain shot, boarding only where the
    /// server's win proof accepts it (missions 03–10).
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

        /// <summary>First line of <see cref="Describe"/>. Defaults to the
        /// hot-seat wording; mission play overrides it so the HUD does not
        /// talk about "sides".</summary>
        public string Headline { get; }

        /// <summary>Whether the ammo toggle does anything; defaults on so
        /// PvP and Mission 10 keep their existing surface.</summary>
        public bool AllowChainShot { get; }

        /// <summary>Whether a target can be boarded; defaults off so PvP
        /// (which defers boarding) never emits a boarding order.</summary>
        public bool AllowBoarding { get; }

        public PvpOrderSession(
            string sideLabel,
            IReadOnlyList<SimShip> ownShips,
            IReadOnlyList<SimShip> enemyShips,
            string headline = null,
            bool allowChainShot = true,
            bool allowBoarding = false)
        {
            SideLabel = sideLabel;
            Headline = headline ?? $"Side {sideLabel} orders:";
            AllowChainShot = allowChainShot;
            AllowBoarding = allowBoarding;
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
            if (draft != null && AllowChainShot)
            {
                draft.Ammo = draft.Ammo == "chain" ? "round" : "chain";
            }
        }

        /// <summary>Swaps the current draft between firing on and boarding
        /// its target. A no-op when the session does not allow boarding.</summary>
        public void ToggleBoarding()
        {
            var draft = CurrentDraft;
            if (draft != null && AllowBoarding)
            {
                draft.Board = !draft.Board;
            }
        }

        /// <summary>
        /// Builds the side's order list. Every order carries the maneuver
        /// deltas (the engine applies them for any action); a set target
        /// makes it a broadside (or a boarding attempt, when boarding is
        /// allowed and chosen), otherwise it stays a pure maneuver. Round
        /// shot omits the ammo key so payloads stay byte-identical to the
        /// legacy shape; boarding orders carry neither side nor ammo.
        /// </summary>
        public List<SimOrder> BuildOrders()
        {
            var orders = new List<SimOrder>(_drafts.Count);
            foreach (var draft in _drafts)
            {
                var hasTarget = draft.TargetShipId != null;
                var boarding = hasTarget && AllowBoarding && draft.Board;
                var broadside = hasTarget && !boarding;
                orders.Add(new SimOrder
                {
                    ShipId = draft.ShipId,
                    Action = boarding ? "boarding" : broadside ? "broadside" : "maneuver",
                    TargetShipId = draft.TargetShipId,
                    Side = broadside ? "starboard" : null,
                    TurnDelta = draft.TurnDelta,
                    SpeedDelta = draft.SpeedDelta,
                    Ammo = broadside && AllowChainShot && draft.Ammo == "chain" ? "chain" : null
                });
            }

            return orders;
        }

        /// <summary>Plain-text summary of the side's drafts for the HUD (TMP-free).</summary>
        public string Describe()
        {
            var builder = new StringBuilder();
            builder.Append(Headline);
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
                    builder.Append(", ").Append(DescribeAttack(draft));
                }
                else
                {
                    builder.Append(", hold fire");
                }
            }

            return builder.ToString();
        }

        /// <summary>Attack clause for a targeted draft ("fire round at X" or
        /// "board X"); shared by the text blob and the structured rows.</summary>
        public string DescribeAttack(PvpOrderDraft draft)
        {
            if (draft?.TargetShipId == null)
            {
                return "hold fire";
            }

            if (AllowBoarding && draft.Board)
            {
                return $"board {draft.TargetShipId}";
            }

            return $"fire {(AllowChainShot ? draft.Ammo : "round")} at {draft.TargetShipId}";
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
