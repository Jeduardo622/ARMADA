using System.Collections.Generic;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// Running ship snapshot for a playable Mission 10 run.
    ///
    /// Mission 10's /resolve returns turn records (events plus a summary) but
    /// no per-turn state, unlike /sim/preview which hands PvpHotseatFlow a
    /// ready-made nextState. The play loop still needs to know where the
    /// ships are and which are still afloat, so this advances a snapshot from
    /// the resolved event stream.
    ///
    /// Every value it writes is one the server already reported — heading from
    /// maneuver events, position from movement events, hull/sail/crew from the
    /// remaining blocks — so it re-derives none of the simulation's decisions
    /// and cannot drift from the server's result. It is a reader of the event
    /// stream, not a second simulation.
    /// </summary>
    public sealed class Mission10BattleState
    {
        private readonly List<SimShip> _ships = new();

        public Mission10BattleState(IReadOnlyList<SimShip> initialShips)
        {
            if (initialShips == null)
            {
                return;
            }

            foreach (var ship in initialShips)
            {
                if (ship?.Id != null)
                {
                    _ships.Add(Clone(ship));
                }
            }
        }

        /// <summary>Snapshot as of the last applied turn.</summary>
        public IReadOnlyList<SimShip> Ships => _ships;

        /// <summary>Deep copy of the snapshot; playback consumers keep their
        /// own turn-start ships, so handing out the live list would let later
        /// turns mutate what an in-flight playback spawned from.</summary>
        public List<SimShip> Snapshot()
        {
            var copy = new List<SimShip>(_ships.Count);
            foreach (var ship in _ships)
            {
                copy.Add(Clone(ship));
            }

            return copy;
        }

        /// <summary>Ships on the given engine side that are still afloat.</summary>
        public List<SimShip> LivingShips(string side)
        {
            var living = new List<SimShip>();
            foreach (var ship in _ships)
            {
                if (ship.Side == side && ship.Hp > 0)
                {
                    living.Add(ship);
                }
            }

            return living;
        }

        /// <summary>
        /// Folds one resolved turn record into the snapshot. Unknown event
        /// types and absent optional fields are ignored: a record only ever
        /// moves the snapshot forward using values it actually carries.
        /// </summary>
        public void Apply(Mission01TurnRecord record)
        {
            if (record?.Events == null)
            {
                return;
            }

            foreach (var simEvent in record.Events)
            {
                switch (simEvent?.Type)
                {
                    case "maneuver":
                        if (simEvent.Heading.HasValue && TryGet(simEvent.ShipId, out var turning))
                        {
                            turning.Heading = simEvent.Heading.Value;
                        }
                        break;
                    case "movement":
                        if (simEvent.Position != null && TryGet(simEvent.ShipId, out var moving))
                        {
                            moving.Position = new SimVector2
                            {
                                X = simEvent.Position.X,
                                Y = simEvent.Position.Y
                            };
                            if (simEvent.Heading.HasValue)
                            {
                                moving.Heading = simEvent.Heading.Value;
                            }
                        }
                        break;
                    case "broadside":
                    case "boarding":
                        ApplyRemaining(simEvent.TargetShipId, simEvent.TargetRemaining);
                        break;
                    case "ram":
                        ApplyRemaining(simEvent.TargetShipId, simEvent.TargetRemaining);
                        // Recoil can sink the rammer, so the rammer's own
                        // remaining block matters for the living-ship set.
                        ApplyRemaining(simEvent.ShipId, simEvent.RammerRemaining);
                        break;
                }
            }
        }

        private void ApplyRemaining(string shipId, SimRemaining remaining)
        {
            if (remaining == null || !TryGet(shipId, out var ship))
            {
                return;
            }

            ship.Hp = remaining.Hp;
            ship.Sail = remaining.Sail;
            ship.Crew = remaining.Crew;
        }

        private bool TryGet(string shipId, out SimShip ship)
        {
            if (shipId != null)
            {
                foreach (var candidate in _ships)
                {
                    if (candidate.Id == shipId)
                    {
                        ship = candidate;
                        return true;
                    }
                }
            }

            ship = null;
            return false;
        }

        private static SimShip Clone(SimShip ship)
        {
            return new SimShip
            {
                Id = ship.Id,
                Side = ship.Side,
                Position = ship.Position == null
                    ? null
                    : new SimVector2 { X = ship.Position.X, Y = ship.Position.Y },
                Heading = ship.Heading,
                Speed = ship.Speed,
                Hp = ship.Hp,
                Sail = ship.Sail,
                Crew = ship.Crew,
                Status = ship.Status,
                Cooldowns = ship.Cooldowns
            };
        }
    }
}
