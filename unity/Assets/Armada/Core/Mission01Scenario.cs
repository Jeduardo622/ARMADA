using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 01 "Fair Wind" scenario. The fingerprint
    /// must match the backend's mission01Fingerprint (src/sim/mission01.ts) so
    /// client and server agree on the exact deterministic scenario.
    /// </summary>
    public static class Mission01Scenario
    {
        public const string MissionCode = "mission-01-fair-wind";
        public const int TurnLimit = 8;
        public const int BonusTurnTarget = 6;
        public const double BonusHullDamageFraction = 0.2;
        public const double EnemyDamageScale = 0.9;
        public const string PlayerShipId = "player-sloop";
        public const string EnemyShipId = "enemy-sloop";

        public static Mission01StartResponse BuildExpectedStart(int seed)
        {
            return new Mission01StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission01Objectives
                {
                    TurnLimit = TurnLimit,
                    BonusTurnTarget = BonusTurnTarget,
                    BonusHullDamageFraction = BonusHullDamageFraction,
                    EnemyDamageScale = EnemyDamageScale
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 0, Speed = 5 },
                    Ships = new List<SimShip>
                    {
                        new SimShip
                        {
                            Id = PlayerShipId,
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
                            Id = EnemyShipId,
                            Side = "enemy",
                            Position = new SimVector2 { X = 150, Y = 0 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 108,
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

        public static string FingerprintOf(Mission01StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|bonusTurns=").Append(start.Objectives.BonusTurnTarget);
            builder.Append("|bonusHull=").Append(start.Objectives.BonusHullDamageFraction.ToString(CultureInfo.InvariantCulture));
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
