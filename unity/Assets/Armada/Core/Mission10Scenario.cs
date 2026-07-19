using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Armada.Client.Core
{
    /// <summary>
    /// Client-side pin of the Mission 10 "Sail-Cutter" scenario. The
    /// fingerprint must match the backend's mission10Fingerprint
    /// (src/sim/mission10.ts) so client and server agree on the exact
    /// deterministic scenario.
    /// </summary>
    public static class Mission10Scenario
    {
        public const string MissionCode = "mission-10-sail-cutter";
        public const int TurnLimit = 10;
        public const int ChainHullPercent = 40;
        public const int ChainSailPercent = 120;
        public const int ChainCrewPercent = 20;
        public const int ChainSailTarget = 60;
        public static readonly string[] PlayerShipIds = { "player-sloop-a", "player-sloop-b" };
        public static readonly string[] EnemyShipIds = { "enemy-clipper-a", "enemy-clipper-b" };

        public static Mission10StartResponse BuildExpectedStart(int seed)
        {
            return new Mission10StartResponse
            {
                MissionCode = MissionCode,
                Seed = seed,
                TurnLimit = TurnLimit,
                Objectives = new Mission10Objectives
                {
                    TurnLimit = TurnLimit,
                    ChainHullPercent = ChainHullPercent,
                    ChainSailPercent = ChainSailPercent,
                    ChainCrewPercent = ChainCrewPercent,
                    ChainSailTarget = ChainSailTarget
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
                            Position = new SimVector2 { X = 220, Y = 35 },
                            Heading = 180,
                            Speed = 3,
                            Hp = 140,
                            Sail = 110,
                            Crew = 50
                        },
                        new SimShip
                        {
                            Id = EnemyShipIds[1],
                            Side = "enemy",
                            Position = new SimVector2 { X = 220, Y = -35 },
                            Heading = 180,
                            Speed = 3,
                            Hp = 140,
                            Sail = 110,
                            Crew = 50
                        }
                    }
                }
            };
        }

        public static string Fingerprint()
        {
            return FingerprintOf(BuildExpectedStart(0));
        }

        public static string FingerprintOf(Mission10StartResponse start)
        {
            if (start?.Objectives == null || start.State?.Wind == null || start.State.Ships == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(start.MissionCode);
            builder.Append("|turnLimit=").Append(start.TurnLimit);
            builder.Append("|chainHull=").Append(start.Objectives.ChainHullPercent);
            builder.Append("|chainSail=").Append(start.Objectives.ChainSailPercent);
            builder.Append("|chainCrew=").Append(start.Objectives.ChainCrewPercent);
            builder.Append("|sailTarget=").Append(start.Objectives.ChainSailTarget);
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
