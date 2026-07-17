using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 04 "Boarding Party" scenario. The
    /// fingerprint must match the backend's mission04Fingerprint
    /// (src/sim/mission04.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission04Scenario
    {
        public const string MissionCode = "mission-04-boarding-party";
        public const int TurnLimit = 10;
        public const double EnemyCrewScale = 0.9;
        public const double PlayerBoardingBonus = 0.1;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-frigate-a", "enemy-frigate-b" };

        public static Mission04StartResponse BuildExpectedStart(int seed)
        {
            return new Mission04StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission04Objectives
                {
                    TurnLimit = TurnLimit,
                    EnemyCrewScale = EnemyCrewScale,
                    PlayerBoardingBonus = PlayerBoardingBonus
                },
                State = new SimState
                {
                    Turn = 1,
                    Wind = new SimWind { Direction = 180, Speed = 3 },
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
                            Sail = 90,
                            Crew = 54
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 260, Y = -40 },
                            Heading = 180,
                            Speed = 2,
                            Hp = 180,
                            Sail = 90,
                            Crew = 54
                        }
                    },
                    SlowZones = new List<SimSlowZone>
                    {
                        new SimSlowZone
                        {
                            Position = new SimVector2 { X = 130, Y = 0 },
                            Radius = 45,
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

        public static string FingerprintOf(Mission04StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|crewScale=").Append(start.Objectives.EnemyCrewScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("|boardBonus=").Append(start.Objectives.PlayerBoardingBonus.ToString(CultureInfo.InvariantCulture));
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
