using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 03 "Raking Shot" scenario. The
    /// fingerprint must match the backend's mission03Fingerprint
    /// (src/sim/mission03.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission03Scenario
    {
        public const string MissionCode = "mission-03-raking-shot";
        public const int TurnLimit = 10;
        public const int BonusTurnTarget = 8;
        public const int RakeHitTarget = 2;
        public const double EnemyDamageScale = 1.05;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-frigate", "enemy-sloop" };

        public static Mission03StartResponse BuildExpectedStart(int seed)
        {
            return new Mission03StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission03Objectives
                {
                    TurnLimit = TurnLimit,
                    BonusTurnTarget = BonusTurnTarget,
                    RakeHitTarget = RakeHitTarget,
                    EnemyDamageScale = EnemyDamageScale
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 90, Speed = 3 },
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
                            Position = new SimVector2 { X = 200, Y = 90 },
                            Heading = 205,
                            Speed = 2,
                            Hp = 189,
                            Sail = 90,
                            Crew = 60
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 200, Y = -90 },
                            Heading = 155,
                            Speed = 3,
                            Hp = 126,
                            Sail = 70,
                            Crew = 40
                        }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission03StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|bonusTurns=").Append(start.Objectives.BonusTurnTarget);
            builder.Append("|rakeTarget=").Append(start.Objectives.RakeHitTarget);
            builder.Append("|enemyScale=").Append(start.Objectives.EnemyDamageScale.ToString(CultureInfo.InvariantCulture));
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
