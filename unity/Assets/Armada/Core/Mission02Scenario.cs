using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 02 "Weather Gage" scenario. The
    /// fingerprint must match the backend's mission02Fingerprint
    /// (src/sim/mission02.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission02Scenario
    {
        public const string MissionCode = "mission-02-weather-gage";
        public const int TurnLimit = 9;
        public const int BonusTurnTarget = 7;
        public const int UpwindBonusTurns = 3;
        public const double EnemyDamageScale = 1;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-aggressor", "enemy-kite" };

        public static Mission02StartResponse BuildExpectedStart(int seed)
        {
            return new Mission02StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission02Objectives
                {
                    TurnLimit = TurnLimit,
                    BonusTurnTarget = BonusTurnTarget,
                    UpwindBonusTurns = UpwindBonusTurns,
                    EnemyDamageScale = EnemyDamageScale
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 90, Speed = 5 },
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
                            Position = new SimVector2 { X = 170, Y = 120 },
                            Heading = 215,
                            Speed = 2,
                            Hp = 120,
                            Sail = 70,
                            Crew = 40
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 220, Y = 160 },
                            Heading = 215,
                            Speed = 2,
                            Hp = 120,
                            Sail = 70,
                            Crew = 40
                        }
                    },
                    Obstacles = new List<SimObstacle>
                    {
                        new SimObstacle { Position = new SimVector2 { X = 100, Y = 40 }, Radius = 25 }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission02StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|bonusTurns=").Append(start.Objectives.BonusTurnTarget);
            builder.Append("|upwindTurns=").Append(start.Objectives.UpwindBonusTurns);
            builder.Append("|enemyScale=").Append(start.Objectives.EnemyDamageScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|wind=").Append(start.State.Wind.Direction).Append(':').Append(start.State.Wind.Speed);

            foreach (var obstacle in start.State.Obstacles ?? new List<SimObstacle>())
            {
                builder.Append("|island=")
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
