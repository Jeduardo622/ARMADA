using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the PvP skirmish "2v2" scenario. The fingerprint
    /// must match the backend's pvpFingerprint (src/sim/pvpScenario.ts) so
    /// client and server agree on the exact deterministic scenario. Side A
    /// rides the engine's 'player' side and side B the 'enemy' side; the v1
    /// modifier set is chain shot only (pinned).
    /// </summary>
    public static class PvpScenario
    {
        public const string ScenarioCode = "pvp-skirmish-2v2";
        public const int TurnLimit = 20;
        public const int DefaultSeed = 11;
        public static readonly string[] SideAShipIds = { "alpha-frigate-a", "alpha-frigate-b" };
        public static readonly string[] SideBShipIds = { "bravo-frigate-a", "bravo-frigate-b" };

        public static SimModifiers BuildModifiers()
        {
            return new SimModifiers { ChainShot = true };
        }

        public static SimState BuildInitialState()
        {
            return new SimState
            {
                Turn = 1,
                Wind = new SimWind { Direction = 90, Speed = 0 },
                Ships = new List<SimShip>
                {
                    BuildFrigate(SideAShipIds[0], "player", 0, 30, 0),
                    BuildFrigate(SideAShipIds[1], "player", 0, -30, 0),
                    BuildFrigate(SideBShipIds[0], "enemy", 220, 30, 180),
                    BuildFrigate(SideBShipIds[1], "enemy", 220, -30, 180)
                }
            };
        }

        private static SimShip BuildFrigate(string id, string side, int x, int y, int heading)
        {
            return new SimShip
            {
                Id = id,
                Side = side,
                Position = new SimVector2 { X = x, Y = y },
                Heading = heading,
                Speed = 3,
                Hp = 120,
                Sail = 80,
                Crew = 50
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildInitialState());
        }

        public static string FingerprintOf(SimState state)
        {
            if (state?.Wind == null || state.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(ScenarioCode);
            builder.Append("|turnLimit=").Append(TurnLimit);
            builder.Append("|modifiers=chainShot");
            builder.Append("|wind=").Append(state.Wind.Direction).Append(':').Append(state.Wind.Speed);

            foreach (var ship in state.Ships.OrderBy(s => s.Id, System.StringComparer.Ordinal))
            {
                builder.Append('|')
                    .Append(ship.Id).Append(':')
                    .Append(ship.Side).Append(':')
                    .Append(ship.Position.X).Append(',').Append(ship.Position.Y)
                    .Append(":h").Append(ship.Heading)
                    .Append(":v").Append(ship.Speed)
                    .Append(":hp").Append(ship.Hp)
                    .Append(":sl").Append(ship.Sail)
                    .Append(":cw").Append(ship.Crew);
            }

            return builder.ToString();
        }
    }
}
