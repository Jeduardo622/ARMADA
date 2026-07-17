using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 05 "Line Break" scenario. The
    /// fingerprint must match the backend's mission05Fingerprint
    /// (src/sim/mission05.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission05Scenario
    {
        public const string MissionCode = "mission-05-line-break";
        public const int TurnLimit = 11;
        public const int BonusTurnTarget = 9;
        public const double FlagshipHpScale = 1.1;
        public const string FlagshipId = "enemy-flagship";
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b", "player-sloop-c" };
        public static readonly string[] EnemyShipIds = { "enemy-flagship", "enemy-escort-a", "enemy-escort-b" };

        public static Mission05StartResponse BuildExpectedStart(int seed)
        {
            return new Mission05StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission05Objectives
                {
                    TurnLimit = TurnLimit,
                    BonusTurnTarget = BonusTurnTarget,
                    FlagshipHpScale = FlagshipHpScale
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 0, Speed = 5 },
                    Ships = new List<SimShip>
                    {
                        new SimShip
                        {
                            Id = PlayerShipIds[0],
                            Side = "player",
                            Position = new SimVector2 { X = 0, Y = 50 },
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
                            Position = new SimVector2 { X = 0, Y = 0 },
                            Heading = 0,
                            Speed = 3,
                            Hp = 120,
                            Sail = 80,
                            Crew = 50
                        },
                        new SimShip
                        {
                            Id = PlayerShipIds[2],
                            Side = "player",
                            Position = new SimVector2 { X = 0, Y = -50 },
                            Heading = 0,
                            Speed = 3,
                            Hp = 120,
                            Sail = 80,
                            Crew = 50
                        },
                        new SimShip
                        {
                            Id = FlagshipId,
                            Side = "enemy",
                            Position = new SimVector2 { X = 260, Y = 0 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 198,
                            Sail = 90,
                            Crew = 60
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 240, Y = 60 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 120,
                            Sail = 70,
                            Crew = 40
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[2],
                            Side = "enemy",
                            Position = new SimVector2 { X = 240, Y = -60 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 120,
                            Sail = 70,
                            Crew = 40
                        }
                    },
                    Obstacles = new List<SimObstacle>
                    {
                        new SimObstacle { Position = new SimVector2 { X = 120, Y = 70 }, Radius = 35 },
                        new SimObstacle { Position = new SimVector2 { X = 120, Y = -70 }, Radius = 35 }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission05StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|bonusTurns=").Append(start.Objectives.BonusTurnTarget);
            builder.Append("|flagshipScale=").Append(start.Objectives.FlagshipHpScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|wind=").Append(start.State.Wind.Direction).Append(':').Append(start.State.Wind.Speed);

            foreach (var obstacle in start.State.Obstacles ?? new List<SimObstacle>())
            {
                builder.Append("|rock=")
                    .Append(obstacle.Position.X).Append(',').Append(obstacle.Position.Y)
                    .Append(":r").Append(obstacle.Radius);
            }

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
