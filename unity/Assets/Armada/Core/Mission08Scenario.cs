using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 08 "Eye of the Wind" scenario. The
    /// fingerprint must match the backend's mission08Fingerprint
    /// (src/sim/mission08.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission08Scenario
    {
        public const string MissionCode = "mission-08-eye-of-the-wind";
        public const int TurnLimit = 10;
        public const int UpwindTurnLimit = 30;
        public const int DownwindTurnLimit = 90;
        public const int SwiftTurnTarget = 8;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-corvette-a", "enemy-corvette-b" };

        public static Mission08StartResponse BuildExpectedStart(int seed)
        {
            return new Mission08StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission08Objectives
                {
                    TurnLimit = TurnLimit,
                    UpwindTurnLimit = UpwindTurnLimit,
                    DownwindTurnLimit = DownwindTurnLimit,
                    SwiftTurnTarget = SwiftTurnTarget
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 180, Speed = 4 },
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
                            Position = new SimVector2 { X = 240, Y = 35 },
                            Heading = 180,
                            Speed = 3,
                            Hp = 150,
                            Sail = 85,
                            Crew = 55
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 240, Y = -35 },
                            Heading = 180,
                            Speed = 3,
                            Hp = 150,
                            Sail = 85,
                            Crew = 55
                        }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission08StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|upwindLimit=").Append(start.Objectives.UpwindTurnLimit);
            builder.Append("|downwindLimit=").Append(start.Objectives.DownwindTurnLimit);
            builder.Append("|swiftTarget=").Append(start.Objectives.SwiftTurnTarget);
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
