using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 06 "Dreadnought Siege" scenario. The
    /// fingerprint must match the backend's mission06Fingerprint
    /// (src/sim/mission06.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission06Scenario
    {
        public const string MissionCode = "mission-06-dreadnought-siege";
        public const int TurnLimit = 14;
        public const int BonusTurnTarget = 12;
        public const double BossHpScale = 1.3;
        public const double BossDamageScale = 1.1;
        public const double EnrageHullFraction = 0.3;
        public const int ReinforcementTurn = 5;
        public const double ReinforcementHpScale = 0.9;
        public const string BossId = "enemy-dreadnought";
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b", "player-sloop-c" };
        public static readonly string[] EnemyShipIds = { "enemy-dreadnought", "enemy-reinforcement" };

        public static Mission06StartResponse BuildExpectedStart(int seed)
        {
            return new Mission06StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission06Objectives
                {
                    TurnLimit = TurnLimit,
                    BonusTurnTarget = BonusTurnTarget,
                    BossHpScale = BossHpScale,
                    BossDamageScale = BossDamageScale,
                    EnrageHullFraction = EnrageHullFraction,
                    ReinforcementTurn = ReinforcementTurn,
                    ReinforcementHpScale = ReinforcementHpScale
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
                            Id = BossId,
                            Side = "enemy",
                            Position = new SimVector2 { X = 280, Y = 0 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 468,
                            Sail = 100,
                            Crew = 80
                        }
                    },
                    SlowZones = new List<SimSlowZone>
                    {
                        new SimSlowZone
                        {
                            Position = new SimVector2 { X = 150, Y = 0 },
                            Radius = 50,
                            SpeedPenalty = 2
                        }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission06StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|bonusTurns=").Append(start.Objectives.BonusTurnTarget);
            builder.Append("|bossScale=").Append(start.Objectives.BossHpScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|bossDmg=").Append(start.Objectives.BossDamageScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|enrage=").Append(start.Objectives.EnrageHullFraction.ToString(CultureInfo.InvariantCulture));
            builder.Append("|reinforce=").Append(start.Objectives.ReinforcementTurn).Append(':')
                .Append(start.Objectives.ReinforcementHpScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|wind=").Append(start.State.Wind.Direction).Append(':').Append(start.State.Wind.Speed);

            foreach (var zone in start.State.SlowZones ?? new List<SimSlowZone>())
            {
                builder.Append("|debris=")
                    .Append(zone.Position.X).Append(',').Append(zone.Position.Y)
                    .Append(":r").Append(zone.Radius)
                    .Append(":p").Append(zone.SpeedPenalty);
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
