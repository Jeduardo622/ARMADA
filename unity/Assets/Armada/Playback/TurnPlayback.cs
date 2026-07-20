using System.Collections.Generic;
using Armada.Client.Core;

namespace Armada.Client.Playback
{
    public enum PlaybackStepKind
    {
        TurnStart,
        Maneuver,
        Move,
        Broadside,
        Ram,
        Boarding,
        Status,
        RunComplete
    }

    /// <summary>
    /// One renderer command decoded from a resolved turn event stream. Plain
    /// data only: positions stay in integer sim coordinates and are scaled to
    /// world units by the renderer. Applied* fields are losses derived from
    /// remaining-block deltas, never nominal damage rolls, so HUD damage
    /// totals match what the sim actually applied (backend precedent:
    /// createAppliedLossTracker in src/sim/missionMetrics.ts).
    /// </summary>
    public sealed class PlaybackStep
    {
        public PlaybackStepKind Kind { get; init; }
        public int Turn { get; init; }
        public string ShipId { get; init; }
        public string TargetShipId { get; init; }
        public int? X { get; init; }
        public int? Y { get; init; }
        public int? Heading { get; init; }
        public bool Hit { get; init; }
        public bool ChainShot { get; init; }
        public int AppliedHull { get; init; }
        public int AppliedSail { get; init; }
        public int AppliedCrew { get; init; }
        // Ram recoil applied to the rammer's own hull (rammerRemaining delta).
        public int SelfAppliedHull { get; init; }
    }

    public sealed class AppliedLossTotals
    {
        public int Hull { get; set; }
        public int Sail { get; set; }
        public int Crew { get; set; }
    }

    /// <summary>
    /// Steps a resolved mission's Mission01TurnRecord list into an ordered
    /// stream of renderer commands. Decoding is lazy so the per-side applied
    /// loss totals grow as steps are consumed, letting a live HUD show
    /// progressive damage. Scene-free plain C# so EditMode tests can drive it
    /// directly.
    /// </summary>
    public sealed class TurnPlayback
    {
        private readonly IReadOnlyList<Mission01TurnRecord> _turns;
        private readonly Dictionary<string, SimRemaining> _remaining = new();
        private readonly Dictionary<string, string> _sides = new();

        private int _turnIndex;
        private int _eventIndex;
        private bool _turnStartEmitted;
        private bool _completeEmitted;

        /// <summary>Losses player-side ships applied to their targets.</summary>
        public AppliedLossTotals PlayerInflicted { get; } = new();

        /// <summary>Losses enemy-side ships applied to their targets.</summary>
        public AppliedLossTotals EnemyInflicted { get; } = new();

        public TurnPlayback(IReadOnlyList<SimShip> initialShips, IReadOnlyList<Mission01TurnRecord> turns)
        {
            _turns = turns;
            if (initialShips == null)
            {
                return;
            }

            foreach (var ship in initialShips)
            {
                if (ship?.Id == null)
                {
                    continue;
                }

                _remaining[ship.Id] = new SimRemaining { Hp = ship.Hp, Sail = ship.Sail, Crew = ship.Crew };
                _sides[ship.Id] = ship.Side;
            }
        }

        /// <summary>
        /// Latest tracked remaining block for a ship: the initial scenario
        /// stats until an event's remaining block snaps them. Powers the
        /// renderer's per-ship HP/sail readouts.
        /// </summary>
        public bool TryGetRemaining(string shipId, out SimRemaining remaining)
        {
            remaining = null;
            return shipId != null && _remaining.TryGetValue(shipId, out remaining);
        }

        /// <summary>
        /// Emits the next renderer command, or false once the stream (plus a
        /// single trailing RunComplete) is exhausted. Unknown or malformed
        /// events are skipped so a newer server event vocabulary cannot stall
        /// playback.
        /// </summary>
        public bool TryStep(out PlaybackStep step)
        {
            while (_turns != null && _turnIndex < _turns.Count)
            {
                var record = _turns[_turnIndex];
                if (record == null)
                {
                    AdvanceTurn();
                    continue;
                }

                if (!_turnStartEmitted)
                {
                    _turnStartEmitted = true;
                    step = new PlaybackStep { Kind = PlaybackStepKind.TurnStart, Turn = record.Turn };
                    return true;
                }

                if (record.Events == null || _eventIndex >= record.Events.Count)
                {
                    AdvanceTurn();
                    continue;
                }

                var simEvent = record.Events[_eventIndex];
                _eventIndex++;
                step = Decode(record.Turn, simEvent);
                if (step != null)
                {
                    return true;
                }
            }

            if (!_completeEmitted)
            {
                _completeEmitted = true;
                step = new PlaybackStep { Kind = PlaybackStepKind.RunComplete };
                return true;
            }

            step = null;
            return false;
        }

        private void AdvanceTurn()
        {
            _turnIndex++;
            _eventIndex = 0;
            _turnStartEmitted = false;
        }

        private PlaybackStep Decode(int turn, SimEvent simEvent)
        {
            switch (simEvent?.Type)
            {
                case "maneuver":
                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Maneuver,
                        Turn = turn,
                        ShipId = simEvent.ShipId,
                        Heading = simEvent.Heading
                    };
                case "movement":
                    if (simEvent.Position == null)
                    {
                        // Payloads without a position cannot be animated.
                        return null;
                    }

                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Move,
                        Turn = turn,
                        ShipId = simEvent.ShipId,
                        X = simEvent.Position.X,
                        Y = simEvent.Position.Y
                    };
                case "broadside":
                {
                    var applied = ApplyRemaining(simEvent.TargetShipId, simEvent.TargetRemaining);
                    Accumulate(simEvent.ShipId, applied);
                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Broadside,
                        Turn = turn,
                        ShipId = simEvent.ShipId,
                        TargetShipId = simEvent.TargetShipId,
                        Hit = simEvent.Hit == true,
                        ChainShot = simEvent.Ammo == "chain",
                        AppliedHull = applied.Hull,
                        AppliedSail = applied.Sail,
                        AppliedCrew = applied.Crew
                    };
                }
                case "ram":
                {
                    var applied = ApplyRemaining(simEvent.TargetShipId, simEvent.TargetRemaining);
                    Accumulate(simEvent.ShipId, applied);
                    // Recoil is self-inflicted, so it updates the rammer's
                    // remaining block but never counts toward either side's
                    // inflicted totals.
                    var recoil = ApplyRemaining(simEvent.ShipId, simEvent.RammerRemaining);
                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Ram,
                        Turn = turn,
                        ShipId = simEvent.ShipId,
                        TargetShipId = simEvent.TargetShipId,
                        Hit = true,
                        AppliedHull = applied.Hull,
                        AppliedSail = applied.Sail,
                        AppliedCrew = applied.Crew,
                        SelfAppliedHull = recoil.Hull
                    };
                }
                case "boarding":
                {
                    var applied = ApplyRemaining(simEvent.TargetShipId, simEvent.TargetRemaining);
                    Accumulate(simEvent.ShipId, applied);
                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Boarding,
                        Turn = turn,
                        ShipId = simEvent.ShipId,
                        TargetShipId = simEvent.TargetShipId,
                        Hit = simEvent.Success == true,
                        AppliedHull = applied.Hull,
                        AppliedSail = applied.Sail,
                        AppliedCrew = applied.Crew
                    };
                }
                case "status":
                    return new PlaybackStep
                    {
                        Kind = PlaybackStepKind.Status,
                        Turn = turn,
                        ShipId = simEvent.ShipId
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Applied loss for one event: the clamped delta between the ship's
        /// tracked remaining block and the event's reported remaining block.
        /// The tracked block then snaps to the reported one so later deltas
        /// stay exact even if an event was skipped.
        /// </summary>
        private AppliedLossTotals ApplyRemaining(string shipId, SimRemaining reported)
        {
            var applied = new AppliedLossTotals();
            if (shipId == null || reported == null || !_remaining.TryGetValue(shipId, out var previous))
            {
                return applied;
            }

            applied.Hull = previous.Hp > reported.Hp ? previous.Hp - reported.Hp : 0;
            applied.Sail = previous.Sail > reported.Sail ? previous.Sail - reported.Sail : 0;
            applied.Crew = previous.Crew > reported.Crew ? previous.Crew - reported.Crew : 0;
            _remaining[shipId] = new SimRemaining { Hp = reported.Hp, Sail = reported.Sail, Crew = reported.Crew };
            return applied;
        }

        private void Accumulate(string attackerShipId, AppliedLossTotals applied)
        {
            if (attackerShipId == null || !_sides.TryGetValue(attackerShipId, out var side))
            {
                return;
            }

            var totals = side == "player" ? PlayerInflicted : EnemyInflicted;
            totals.Hull += applied.Hull;
            totals.Sail += applied.Sail;
            totals.Crew += applied.Crew;
        }
    }

    /// <summary>
    /// Scene-free interpolation helper for the renderer: normalized smoothstep
    /// progress over a step's duration.
    /// </summary>
    public static class PlaybackEase
    {
        public static float Progress(float elapsed, float duration)
        {
            if (duration <= 0f || elapsed >= duration)
            {
                return 1f;
            }
            if (elapsed <= 0f)
            {
                return 0f;
            }

            var t = elapsed / duration;
            return t * t * (3f - 2f * t);
        }
    }
}
