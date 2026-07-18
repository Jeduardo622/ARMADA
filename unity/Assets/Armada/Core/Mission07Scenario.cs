using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 07 "Burning Seas" scenario. The
    /// fingerprint must match the backend's mission07Fingerprint
    /// (src/sim/mission07.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission07Scenario
    {
        public const string MissionCode = "mission-07-burning-seas";
        public const int TurnLimit = 10;
        public const double EnemySailScale = 0.85;
        public const int IgnitionTarget = 1;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-frigate-a", "enemy-frigate-b" };

        public static Mission07StartResponse BuildExpectedStart(int seed)
        {
            return new Mission07StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission07Objectives
                {
                    TurnLimit = TurnLimit,
                    EnemySailScale = EnemySailScale,
                    IgnitionTarget = IgnitionTarget
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 0, Speed = 4 },
                    Ships = new List<SimShip>
                    {
                        new SimShip
                        {
                            Id = PlayerShipIds[0],
                            Side = "player",
                            Position = new SimVector2 { X = 0, Y = 30 },
                            Heading = 0,
                            Speed = 3,
                            Hp = 120,
                            Sail = 80,
                            Crew = 50
                        },
                        new SimShip
                        {
                            Id = PlayerShipIds[1],
                            Side = "player",
                            Position = new SimVector2 { X = 0, Y = -30 },
                            Heading = 0,
                            Speed = 3,
                            Hp = 120,
                            Sail = 80,
                            Crew = 50
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[0],
                            Side = "enemy",
                            Position = new SimVector2 { X = 220, Y = 40 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 180,
                            Sail = 76,
                            Crew = 60
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 220, Y = -40 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 180,
                            Sail = 76,
                            Crew = 60
                        }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission07StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|sailScale=").Append(start.Objectives.EnemySailScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|ignitionTarget=").Append(start.Objectives.IgnitionTarget);
            builder.Append("|wind=").Append(start.State.Wind.Direction).Append(':').Append(start.State.Wind.Speed);

            foreach (var ship in start.State.Ships.OrderBy(s => s.Id, System.StringComparer.Ordinal))
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
