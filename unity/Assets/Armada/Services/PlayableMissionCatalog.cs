using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>One bonus objective of a resolved run, as the HUD shows it.</summary>
    public sealed class PlayableBonus
    {
        public string Label { get; init; }
        public bool Achieved { get; init; }
    }

    /// <summary>
    /// Mission-agnostic view of a resolved run. Every mission's outcome shares
    /// these fields; only the bonus objectives differ in name, so the adapter
    /// flattens them into labelled flags.
    /// </summary>
    public sealed class PlayableMissionOutcome
    {
        public string MissionCode { get; init; }
        public int Seed { get; init; }
        public string Result { get; init; }
        public string FailReason { get; init; }
        public int TurnCount { get; init; }
        public int TurnLimit { get; init; }
        public List<Mission01TurnRecord> Turns { get; init; }
        public List<PlayableBonus> Bonuses { get; init; }

        /// <summary>The GDD's 3-star rating: one for the win, one per bonus
        /// objective achieved (every mission carries two). Zero on a loss.</summary>
        public int Stars
        {
            get
            {
                if (Result != "win")
                {
                    return 0;
                }

                var stars = 1;
                if (Bonuses != null)
                {
                    foreach (var bonus in Bonuses)
                    {
                        if (bonus != null && bonus.Achieved)
                        {
                            stars++;
                        }
                    }
                }

                return stars;
            }
        }
    }

    public sealed class PlayableRunResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public PlayableMissionOutcome Outcome { get; init; }
    }

    /// <summary>
    /// Resolves (and completes) one mission for the mission-agnostic play
    /// loop. Wraps the mission's own client flow, so the scenario-parity,
    /// seed, and outcome checks each flow already performs stay in force.
    /// </summary>
    public interface IPlayableMissionRunner
    {
        Task<PlayableRunResult> RunAsync(int seed, List<List<SimOrder>> turns);

        /// <summary>Completes the last resolved run, re-sending the exact
        /// seed and turns it was resolved with as the server's win proof.</summary>
        Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string playerId, Dictionary<string, object> result);
    }

    /// <summary>
    /// Static description of a mission for the campaign: what the menu shows
    /// and which order surfaces the play loop offers. Rules come from the
    /// mission's mirrored scenario and the server's win-proof config
    /// (src/routes/missions.ts): boarding is offered exactly where the proof
    /// accepts it (missions 03–10), the ammo toggle only where the mission
    /// runs chain shot (Mission 10).
    /// </summary>
    public sealed class PlayableMissionInfo
    {
        private readonly Func<int, SimState> _startState;
        private readonly Dictionary<int, List<SimShip>> _arrivals;

        public PlayableMissionInfo(
            int number,
            string code,
            string title,
            string briefing,
            int turnLimit,
            int playableSeed,
            bool allowsBoarding,
            bool allowsChainShot,
            Func<int, SimState> startState,
            Dictionary<int, List<SimShip>> arrivals = null)
        {
            Number = number;
            Code = code;
            Title = title;
            Briefing = briefing;
            TurnLimit = turnLimit;
            PlayableSeed = playableSeed;
            AllowsBoarding = allowsBoarding;
            AllowsChainShot = allowsChainShot;
            _startState = startState;
            _arrivals = arrivals ?? new Dictionary<int, List<SimShip>>();
        }

        public int Number { get; }
        public string Code { get; }
        public string Title { get; }
        public string Briefing { get; }
        public int TurnLimit { get; }

        /// <summary>
        /// Seed the campaign plays. The server's default seed for each mission,
        /// except Mission 10, whose free-play seed 872 was chosen from a sweep
        /// over plausible order families (docs/demo.md).
        /// </summary>
        public int PlayableSeed { get; }
        public bool AllowsBoarding { get; }
        public bool AllowsChainShot { get; }

        public string DisplayName => $"Mission {Number:00}: {Title}";

        /// <summary>Opening state from the client's mirrored scenario — the
        /// same state each flow pins against the server's /start.</summary>
        public SimState BuildStartState(int seed) => _startState(seed);

        /// <summary>Ships the server splices in at the start of the given
        /// turn (no event announces them); empty for most missions.</summary>
        public IReadOnlyList<SimShip> ArrivalsAt(int turn)
        {
            return _arrivals.TryGetValue(turn, out var ships) ? ships : (IReadOnlyList<SimShip>)Array.Empty<SimShip>();
        }

        /// <summary>Bar maxima for every ship that can appear in the battle:
        /// the opening fleet plus every scripted arrival.</summary>
        public List<SimShip> BaselineShips(int seed)
        {
            var ships = new List<SimShip>(BuildStartState(seed)?.Ships ?? new List<SimShip>());
            foreach (var arrival in _arrivals.Values)
            {
                ships.AddRange(arrival);
            }

            return ships;
        }
    }

    /// <summary>
    /// The ten campaign missions and the adapters that let one play loop
    /// drive any of them. Missions 01–06 have no flow-side completion, so
    /// their runner keeps the proof itself; missions 07–10 complete through
    /// their flow (Mission 07's flow also carries owned upgrade tiers).
    /// </summary>
    public static class PlayableMissionCatalog
    {
        public const int PlayableSeedMission10 = 872;

        private static readonly List<PlayableMissionInfo> Missions = new()
        {
            new PlayableMissionInfo(
                1, Mission01Scenario.MissionCode, "Fair Wind",
                "Tutorial skirmish: learn how the wind drives your sloop, then sink the raider.",
                Mission01Scenario.TurnLimit, 101, allowsBoarding: false, allowsChainShot: false,
                seed => Mission01Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                2, Mission02Scenario.MissionCode, "Weather Gage",
                "Win the upwind position and dictate the engagement.",
                Mission02Scenario.TurnLimit, 202, allowsBoarding: false, allowsChainShot: false,
                seed => Mission02Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                3, Mission03Scenario.MissionCode, "Raking Shot",
                "Cross the enemy's bow or stern: raking fire hits half again as hard.",
                Mission03Scenario.TurnLimit, 303, allowsBoarding: true, allowsChainShot: false,
                seed => Mission03Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                4, Mission04Scenario.MissionCode, "Boarding Party",
                "Close in and board: your crews fight harder than theirs, but a failed boarding costs men.",
                Mission04Scenario.TurnLimit, 404, allowsBoarding: true, allowsChainShot: false,
                seed => Mission04Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                5, Mission05Scenario.MissionCode, "Line Break",
                "Break the enemy line and coordinate your broadsides on the flagship.",
                Mission05Scenario.TurnLimit, 505, allowsBoarding: true, allowsChainShot: false,
                seed => Mission05Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                6, Mission06Scenario.MissionCode, "Dreadnought Siege",
                "Boss battle: wear down the dreadnought. Reinforcements arrive on turn "
                    + Mission06Scenario.ReinforcementTurn + ".",
                Mission06Scenario.TurnLimit, 606, allowsBoarding: true, allowsChainShot: false,
                seed => Mission06Scenario.BuildExpectedStart(seed).State,
                new Dictionary<int, List<SimShip>>
                {
                    [Mission06Scenario.ReinforcementTurn] = new List<SimShip> { Mission06Scenario.BuildReinforcement() }
                }),
            new PlayableMissionInfo(
                7, Mission07Scenario.MissionCode, "Burning Seas",
                "Fire spreads and rigging fails under sustained fire. Set them ablaze; stay unscorched.",
                Mission07Scenario.TurnLimit, 707, allowsBoarding: true, allowsChainShot: false,
                seed => Mission07Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                8, Mission08Scenario.MissionCode, "Eye of the Wind",
                "The helm answers slowly beating into the wind and freely running before it.",
                Mission08Scenario.TurnLimit, 808, allowsBoarding: true, allowsChainShot: false,
                seed => Mission08Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                9, Mission09Scenario.MissionCode, "Iron Bow",
                "A hull driven home is a weapon. Sail within ram range, and mind their bows.",
                Mission09Scenario.TurnLimit, 909, allowsBoarding: true, allowsChainShot: false,
                seed => Mission09Scenario.BuildExpectedStart(seed).State),
            new PlayableMissionInfo(
                10, Mission10Scenario.MissionCode, "Sail-Cutter",
                "Ball sinks ships, chain cuts their sails. Pick the right load for each broadside.",
                Mission10Scenario.TurnLimit, PlayableSeedMission10, allowsBoarding: true, allowsChainShot: true,
                seed => Mission10Scenario.BuildExpectedStart(seed).State)
        };

        public static IReadOnlyList<PlayableMissionInfo> All => Missions;

        public static PlayableMissionInfo ByNumber(int number)
        {
            foreach (var mission in Missions)
            {
                if (mission.Number == number)
                {
                    return mission;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the runner for a mission against the live service graph.
        /// <paramref name="upgrades"/> is only consulted by Mission 07, the one
        /// mission whose simulation applies owned upgrade tiers.
        /// </summary>
        public static IPlayableMissionRunner CreateRunner(
            int number,
            MissionService missions,
            DeterministicSimHooks hooks = null,
            IUpgradesClient upgrades = null)
        {
            return number switch
            {
                1 => ForMission01(new Mission01Flow(missions, hooks), missions),
                2 => ForMission02(new Mission02Flow(missions, hooks), missions),
                3 => ForMission03(new Mission03Flow(missions, hooks), missions),
                4 => ForMission04(new Mission04Flow(missions, hooks), missions),
                5 => ForMission05(new Mission05Flow(missions, hooks), missions),
                6 => ForMission06(new Mission06Flow(missions, hooks), missions),
                7 => ForMission07(new Mission07Flow(missions, hooks, upgrades, missions)),
                8 => ForMission08(new Mission08Flow(missions, hooks, missions)),
                9 => ForMission09(new Mission09Flow(missions, hooks, missions)),
                10 => ForMission10(new Mission10Flow(missions, hooks, missions)),
                _ => null
            };
        }

        public static IPlayableMissionRunner ForMission01(Mission01Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission01Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("Under " + Percent(Mission01Scenario.BonusHullDamageFraction) + " hull damage", o.BonusObjectives?.UnderHullDamageThreshold),
                    Bonus("Won within " + Mission01Scenario.BonusTurnTarget + " turns", o.BonusObjectives?.WithinTurnTarget)));
            });
        }

        public static IPlayableMissionRunner ForMission02(Mission02Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission02Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("Held the weather gage " + Mission02Scenario.UpwindBonusTurns + " turns", o.BonusObjectives?.HeldWeatherGage),
                    Bonus("Won within " + Mission02Scenario.BonusTurnTarget + " turns", o.BonusObjectives?.WithinTurnTarget)));
            });
        }

        public static IPlayableMissionRunner ForMission03(Mission03Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission03Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("Landed " + Mission03Scenario.RakeHitTarget + " raking hits", o.BonusObjectives?.LandedRakingHits),
                    Bonus("Won within " + Mission03Scenario.BonusTurnTarget + " turns", o.BonusObjectives?.WithinTurnTarget)));
            });
        }

        public static IPlayableMissionRunner ForMission04(Mission04Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission04Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("Successful boarding", o.BonusObjectives?.SuccessfulBoarding),
                    Bonus("No ship lost", o.BonusObjectives?.NoShipLost)));
            });
        }

        public static IPlayableMissionRunner ForMission05(Mission05Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission05Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("Sank the flagship first", o.BonusObjectives?.SankFlagshipFirst),
                    Bonus("Won within " + Mission05Scenario.BonusTurnTarget + " turns", o.BonusObjectives?.WithinTurnTarget)));
            });
        }

        public static IPlayableMissionRunner ForMission06(Mission06Flow flow, IMissionCompletionClient completion)
        {
            return ProofRunner(Mission06Scenario.MissionCode, completion, async (seed, turns) =>
            {
                var run = await flow.RunAsync(seed, turns);
                var o = run.Outcome;
                return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                    o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                    Bonus("No ship lost", o.BonusObjectives?.NoShipLost),
                    Bonus("Won within " + Mission06Scenario.BonusTurnTarget + " turns", o.BonusObjectives?.WithinTurnTarget)));
            });
        }

        public static IPlayableMissionRunner ForMission07(Mission07Flow flow)
        {
            return FlowRunner(
                async (seed, turns) =>
                {
                    var run = await flow.RunAsync(seed, turns);
                    var o = run.Outcome;
                    return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                        o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                        Bonus("Set an enemy ablaze", o.BonusObjectives?.EnemyIgnited),
                        Bonus("Won unscorched", o.BonusObjectives?.Unscorched)));
                },
                (playerId, result) => flow.CompleteAsync(playerId, result));
        }

        public static IPlayableMissionRunner ForMission08(Mission08Flow flow)
        {
            return FlowRunner(
                async (seed, turns) =>
                {
                    var run = await flow.RunAsync(seed, turns);
                    var o = run.Outcome;
                    return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                        o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                        Bonus("Clean tack (no clamped maneuver)", o.BonusObjectives?.CleanTack),
                        Bonus("Won within " + Mission08Scenario.SwiftTurnTarget + " turns", o.BonusObjectives?.SwiftVictory)));
                },
                (playerId, result) => flow.CompleteAsync(playerId, result));
        }

        public static IPlayableMissionRunner ForMission09(Mission09Flow flow)
        {
            return FlowRunner(
                async (seed, turns) =>
                {
                    var run = await flow.RunAsync(seed, turns);
                    var o = run.Outcome;
                    return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                        o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                        Bonus("Hull breaker (" + Mission09Scenario.RamTarget + " rams)", o.BonusObjectives?.HullBreaker),
                        Bonus("Unrammed", o.BonusObjectives?.Unrammed)));
                },
                (playerId, result) => flow.CompleteAsync(playerId, result));
        }

        public static IPlayableMissionRunner ForMission10(Mission10Flow flow)
        {
            return FlowRunner(
                async (seed, turns) =>
                {
                    var run = await flow.RunAsync(seed, turns);
                    var o = run.Outcome;
                    return Map(run.Success, run.FailureReason, o == null ? null : Outcome(
                        o.MissionCode, o.Seed, o.Result, o.FailReason, o.TurnCount, o.TurnLimit, o.Turns,
                        Bonus("Sail shredder (" + Mission10Scenario.ChainSailTarget + " chain sail damage)", o.BonusObjectives?.SailShredder),
                        Bonus("Mixed battery", o.BonusObjectives?.MixedBattery)));
                },
                (playerId, result) => flow.CompleteAsync(playerId, result));
        }

        /// <summary>Deep copy of an order array, so the proof a runner keeps
        /// cannot be mutated by the caller after the resolve.</summary>
        public static List<List<SimOrder>> CopyTurns(List<List<SimOrder>> turns)
        {
            var copy = new List<List<SimOrder>>();
            if (turns == null)
            {
                return copy;
            }

            foreach (var turn in turns)
            {
                if (turn == null)
                {
                    copy.Add(null);
                    continue;
                }

                var orders = new List<SimOrder>(turn.Count);
                foreach (var order in turn)
                {
                    orders.Add(order == null ? null : new SimOrder
                    {
                        ShipId = order.ShipId,
                        Action = order.Action,
                        TargetShipId = order.TargetShipId,
                        TurnDelta = order.TurnDelta,
                        SpeedDelta = order.SpeedDelta,
                        Side = order.Side,
                        Ammo = order.Ammo
                    });
                }

                copy.Add(orders);
            }

            return copy;
        }

        // Missions 01–06: the runner keeps the proof (seed + snapshotted
        // turns) and completes through the completion client directly, the
        // same request MissionUIController.CompleteMission sends.
        private static IPlayableMissionRunner ProofRunner(
            string code,
            IMissionCompletionClient completion,
            Func<int, List<List<SimOrder>>, Task<PlayableRunResult>> run)
        {
            return new Runner(run, (playerId, result, seed, turns) =>
            {
                if (completion == null)
                {
                    return Task.FromResult(new ServiceResult<MissionCompleteResponse>
                    {
                        Success = false,
                        ErrorReason = "completion_client_missing"
                    });
                }

                return completion.CompleteAsync(code, new MissionCompleteRequest
                {
                    PlayerId = playerId,
                    Result = result,
                    Seed = seed,
                    Turns = turns
                });
            });
        }

        // Missions 07–10: the flow already snapshots its resolved run (and,
        // for Mission 07, the upgrade tiers) and completes from it.
        private static IPlayableMissionRunner FlowRunner(
            Func<int, List<List<SimOrder>>, Task<PlayableRunResult>> run,
            Func<string, Dictionary<string, object>, Task<ServiceResult<MissionCompleteResponse>>> complete)
        {
            return new Runner(run, (playerId, result, seed, turns) => complete(playerId, result));
        }

        private static PlayableRunResult Map(bool success, string failureReason, PlayableMissionOutcome outcome)
        {
            if (!success || outcome == null)
            {
                return new PlayableRunResult { Success = false, FailureReason = failureReason ?? "resolve_failed" };
            }

            return new PlayableRunResult { Success = true, Outcome = outcome };
        }

        private static PlayableMissionOutcome Outcome(
            string code,
            int seed,
            string result,
            string failReason,
            int turnCount,
            int turnLimit,
            List<Mission01TurnRecord> turns,
            params PlayableBonus[] bonuses)
        {
            return new PlayableMissionOutcome
            {
                MissionCode = code,
                Seed = seed,
                Result = result,
                FailReason = failReason,
                TurnCount = turnCount,
                TurnLimit = turnLimit,
                Turns = turns,
                Bonuses = new List<PlayableBonus>(bonuses)
            };
        }

        private static PlayableBonus Bonus(string label, bool? achieved)
        {
            return new PlayableBonus { Label = label, Achieved = achieved == true };
        }

        private static string Percent(double fraction)
        {
            return ((int)Math.Round(fraction * 100)) + "%";
        }

        private sealed class Runner : IPlayableMissionRunner
        {
            private readonly Func<int, List<List<SimOrder>>, Task<PlayableRunResult>> _run;
            private readonly Func<string, Dictionary<string, object>, int, List<List<SimOrder>>, Task<ServiceResult<MissionCompleteResponse>>> _complete;

            private int _resolvedSeed;
            private List<List<SimOrder>> _resolvedTurns;
            private bool _hasResolvedRun;

            public Runner(
                Func<int, List<List<SimOrder>>, Task<PlayableRunResult>> run,
                Func<string, Dictionary<string, object>, int, List<List<SimOrder>>, Task<ServiceResult<MissionCompleteResponse>>> complete)
            {
                _run = run;
                _complete = complete;
            }

            public async Task<PlayableRunResult> RunAsync(int seed, List<List<SimOrder>> turns)
            {
                _hasResolvedRun = false;

                // Snapshot first: the proof must be the turns this resolve
                // used, not whatever the caller's list holds later.
                var runTurns = CopyTurns(turns);
                var run = await _run(seed, runTurns);
                if (run.Success)
                {
                    _resolvedSeed = seed;
                    _resolvedTurns = runTurns;
                    _hasResolvedRun = true;
                }

                return run;
            }

            public Task<ServiceResult<MissionCompleteResponse>> CompleteAsync(string playerId, Dictionary<string, object> result)
            {
                if (!_hasResolvedRun)
                {
                    return Task.FromResult(new ServiceResult<MissionCompleteResponse>
                    {
                        Success = false,
                        ErrorReason = "no_resolved_run"
                    });
                }

                return _complete(playerId, result, _resolvedSeed, _resolvedTurns);
            }
        }
    }
}
