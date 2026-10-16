using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using TMPro;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Mission-agnostic playable loop for the campaign: the player authors
    /// orders for their surviving ships one turn at a time, the turn resolves
    /// server-side, and the spectator renderer plays back just that turn
    /// before the next order round. Generalizes Mission10PlayController to
    /// any <see cref="PlayableMissionInfo"/>.
    ///
    /// The client never simulates. Each confirmed turn is appended to a
    /// client-held order array and the whole array is re-sent to the
    /// mission's /resolve, which accepts a partial turns list. Every mission
    /// runs through runMissionLoop (src/sim/missionRunner.ts), which feeds
    /// turn N only the previous turn's state and playerTurnOrders[N-1], so
    /// resolving the prefix [t1..tN] returns byte-identical records for turns
    /// 1..N no matter what follows. Only the newest record is rendered; the
    /// tail the server simulates past the authored prefix is ignored.
    ///
    /// Because the accumulated array is exactly what produced the win, it is
    /// also the completion proof: the runner snapshots it on each resolve and
    /// re-sends the winning snapshot on completion.
    ///
    /// Button handlers are public so the generated scene wires uGUI buttons
    /// to them; order state lives in plain-C# collaborators (PvpOrderSession /
    /// MissionBattleState) and the TMP labels are optional so tests never
    /// touch TMP components.
    /// </summary>
    public sealed class MissionPlayController : MonoBehaviour
    {
        public enum PlayPhase
        {
            Idle,
            OrderEntry,
            Resolving,
            Playback,
            Finished
        }

        [Header("UI Wiring (optional)")]
        [SerializeField] private TMP_Text orderLabel;
        [Tooltip("Structured per-ship order rows (W4 HUD IA); optional — null keeps the text blob.")]
        [SerializeField] private OrderPanelView orderPanel;
        [SerializeField] private TMP_Text statusLabel;
        [Tooltip("Ammo button; shown only for missions that run chain shot.")]
        [SerializeField] private GameObject ammoControl;
        [Tooltip("Board button; shown only for missions whose win proof accepts boarding.")]
        [SerializeField] private GameObject boardingControl;

        private PlayableMissionInfo _mission;
        private IPlayableMissionRunner _runner;
        private SpectatorRenderer _spectator;
        private MissionUIController _missionUI;
        private int _seed;
        private SimState _startState;
        private List<SimShip> _baselineShips;

        private readonly List<List<SimOrder>> _authoredTurns = new();
        private readonly List<Mission01TurnRecord> _resolvedRecords = new();
        private MissionBattleState _battle;
        private PvpOrderSession _session;
        private PlayableMissionOutcome _lastOutcome;
        private bool _missionOver;

        /// <summary>Raised once when a run ends (win, loss, or a resolve
        /// failure, in which case the outcome is null).</summary>
        public event Action<PlayableMissionInfo, PlayableMissionOutcome> MissionFinished;

        public PlayPhase Phase { get; private set; } = PlayPhase.Idle;

        public PlayableMissionInfo Mission => _mission;

        /// <summary>Order session for the turn being authored; test hook.</summary>
        public PvpOrderSession CurrentSession => _session;

        /// <summary>1-based number of the turn being authored; test hook.</summary>
        public int TurnNumber => _authoredTurns.Count + 1;

        /// <summary>Outcome of the last resolve; test hook.</summary>
        public PlayableMissionOutcome LastOutcome => _lastOutcome;

        /// <summary>Ship snapshot as of the last applied turn; test hook.</summary>
        public IReadOnlyList<SimShip> BattleShips => _battle?.Ships;

        public string LastError { get; private set; }

        /// <summary>In-flight turn submission; test hook.</summary>
        public Task ActiveSubmit { get; private set; }

        /// <summary>In-flight completion of a won run; test hook.</summary>
        public Task<ServiceResult<MissionCompleteResponse>> CompletionTask { get; private set; }

        public void Compose(
            PlayableMissionInfo mission,
            IPlayableMissionRunner runner,
            SpectatorRenderer spectator,
            MissionUIController missionUI,
            int? seed = null)
        {
            _mission = mission;
            _runner = runner;
            _spectator = spectator;
            _missionUI = missionUI;
            _seed = seed ?? mission?.PlayableSeed ?? 0;
        }

        public void BeginMission()
        {
            if (_mission == null || _runner == null)
            {
                SetStatus("Mission not composed.");
                return;
            }

            _authoredTurns.Clear();
            _resolvedRecords.Clear();
            _lastOutcome = null;
            _missionOver = false;
            LastError = null;
            CompletionTask = null;
            ActiveSubmit = null;
            // Offer only the order surfaces this mission accepts.
            if (ammoControl != null)
            {
                ammoControl.SetActive(_mission.AllowsChainShot);
            }
            if (boardingControl != null)
            {
                boardingControl.SetActive(_mission.AllowsBoarding);
            }

            _startState = _mission.BuildStartState(_seed);
            _baselineShips = _mission.BaselineShips(_seed);
            RebuildBattle();

            // Show the opening position before the first orders are written:
            // the player is aiming at this board, so it must be on screen.
            ShowBoard($"{_mission.DisplayName} — {_mission.Briefing}");
            BeginOrderEntry();
        }

        // --- Button handlers (wired by CampaignSceneBuilder) ---

        public void OnTurnLeft() => WithSession(session => session.AdjustTurn(-1));

        public void OnTurnRight() => WithSession(session => session.AdjustTurn(1));

        public void OnSpeedUp() => WithSession(session => session.AdjustSpeed(1));

        public void OnSpeedDown() => WithSession(session => session.AdjustSpeed(-1));

        public void OnCycleTarget() => WithSession(session => session.CycleTarget());

        public void OnToggleAmmo() => WithSession(session => session.ToggleAmmo());

        public void OnToggleBoarding() => WithSession(session => session.ToggleBoarding());

        public void OnNextShip() => WithSession(session => session.NextShip());

        public void OnConfirmTurn()
        {
            if (Phase != PlayPhase.OrderEntry || _session == null)
            {
                return;
            }

            _authoredTurns.Add(_session.BuildOrders());
            _session = null;
            Phase = PlayPhase.Resolving;
            SetStatus($"Resolving turn {_authoredTurns.Count}...");
            SetOrderText(string.Empty);
            ActiveSubmit = ResolveTurnAsync();
        }

        /// <summary>
        /// Drops the last confirmed turn and rewinds to authoring it again.
        /// The order array is client-side and the server holds no run state,
        /// so undo costs nothing: the next resolve simply sends a shorter
        /// prefix. Rewinding the ship snapshot replays the retained records,
        /// which prefix stability guarantees are the same ones a re-resolve
        /// would return.
        /// </summary>
        public void OnUndoTurn()
        {
            if (Phase != PlayPhase.OrderEntry || _authoredTurns.Count == 0)
            {
                return;
            }

            _authoredTurns.RemoveAt(_authoredTurns.Count - 1);
            if (_resolvedRecords.Count > 0)
            {
                _resolvedRecords.RemoveAt(_resolvedRecords.Count - 1);
            }

            RebuildBattle();

            // The renderer still shows the withdrawn turn's end positions and
            // damage; rewind it too, or the replacement orders are written
            // against a board a turn ahead of the retained prefix.
            ShowBoard(_resolvedRecords.Count == 0
                ? "Turn 1 orders withdrawn — board rewound to the opening position."
                : $"Turn {_authoredTurns.Count + 1} orders withdrawn — board rewound to the end of turn {_resolvedRecords.Count}.");

            SetStatus($"Turn {_authoredTurns.Count + 1} orders withdrawn — re-enter them.");
            BeginOrderEntry();
        }

        /// <summary>Starts the same mission over from the opening position
        /// once a run has ended.</summary>
        public void OnRetryMission()
        {
            if (Phase == PlayPhase.Finished)
            {
                BeginMission();
            }
        }

        /// <summary>
        /// Advances out of the playback phase once the spectator finishes.
        /// Called every frame from Update; public so inactive-object tests
        /// can drive the transition manually.
        /// </summary>
        public void PollPlayback()
        {
            if (Phase != PlayPhase.Playback)
            {
                return;
            }

            if (_spectator == null || _spectator.IsFinished)
            {
                AdvanceAfterTurn();
            }
        }

        private void Update()
        {
            PollPlayback();
        }

        // Rebuilds the snapshot from the opening fleet and the retained
        // records, splicing each scripted arrival in at the start of its turn
        // exactly where the server's onTurnStart hook does.
        private void RebuildBattle()
        {
            _battle = new MissionBattleState(_startState?.Ships);
            for (var i = 0; i < _resolvedRecords.Count; i++)
            {
                AddArrivals(i + 1);
                _battle.Apply(_resolvedRecords[i]);
            }
        }

        private void AddArrivals(int turn)
        {
            foreach (var ship in _mission.ArrivalsAt(turn))
            {
                _battle.AddShip(ship);
            }
        }

        // Puts the renderer on the current battle snapshot without queuing
        // playback. Bar maxima stay pinned to the battle-start stats.
        private void ShowBoard(string hudLine)
        {
            if (_spectator != null)
            {
                _spectator.ShowBoard(
                    _battle.Snapshot(),
                    hudLine,
                    _baselineShips,
                    _startState?.Obstacles,
                    _startState?.SlowZones,
                    _startState?.Wind);
            }
        }

        private async Task ResolveTurnAsync()
        {
            var authoredCount = _authoredTurns.Count;
            PlayableRunResult run;
            try
            {
                run = await _runner.RunAsync(_seed, _authoredTurns);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                run = new PlayableRunResult { Success = false, FailureReason = "resolve_exception" };
            }

            if (!run.Success || run.Outcome?.Turns == null)
            {
                Fail(run.FailureReason ?? "resolve_failed");
                return;
            }

            var outcome = run.Outcome;
            if (outcome.Turns.Count < authoredCount)
            {
                // The prefix must produce a record per authored turn; fewer
                // means the contract this loop rests on has changed.
                Fail("missing_turn_record");
                return;
            }

            _lastOutcome = outcome;
            var record = outcome.Turns[authoredCount - 1];
            var shipsAtTurnStart = _battle.Snapshot();
            _battle.Apply(record);
            _resolvedRecords.Add(record);

            // The server always simulates to the turn limit, resolving the
            // turns past the authored prefix with no player orders. Those
            // ghost turns are not the player's run: the battle is only really
            // over when it ended at or before the last authored turn (or the
            // player has now authored the whole limit).
            _missionOver = outcome.TurnCount <= authoredCount;

            if (_spectator != null)
            {
                _spectator.BeginTurns(
                    shipsAtTurnStart,
                    new List<Mission01TurnRecord> { record },
                    _mission.TurnLimit,
                    $"Turn {record.Turn}/{_mission.TurnLimit}: the fleets engage...",
                    CompletionLineFor(record),
                    _baselineShips,
                    _startState?.Obstacles,
                    _startState?.SlowZones,
                    _startState?.Wind);
                Phase = PlayPhase.Playback;
                SetStatus($"Turn {record.Turn} resolved — watching playback.");
            }
            else
            {
                Phase = PlayPhase.Playback;
                AdvanceAfterTurn();
            }
        }

        private void Fail(string reason)
        {
            LastError = reason;
            Phase = PlayPhase.Finished;
            SetStatus($"Turn failed: {LastError}. Retry the mission or return to the mission list.");
            SetOrderText(string.Empty);
            MissionFinished?.Invoke(_mission, null);
        }

        private void AdvanceAfterTurn()
        {
            if (!_missionOver)
            {
                BeginOrderEntry();
                return;
            }

            Finish();
        }

        private void Finish()
        {
            Phase = PlayPhase.Finished;
            var outcome = _lastOutcome;
            if (outcome?.Result == "win")
            {
                SetStatus(
                    $"VICTORY at turn {outcome.TurnCount}/{outcome.TurnLimit} — {outcome.Stars}/3 stars. "
                    + DescribeBonuses(outcome) + " Saving...");
                // Completion goes through the runner so the proof re-sends
                // the exact seed and turns this run was resolved with — the
                // array the player authored.
                if (_missionUI != null)
                {
                    CompletionTask = _missionUI.CompletePlayableAsync(
                        _mission.Code,
                        _runner,
                        new Dictionary<string, object> { ["outcome"] = "win", ["stars"] = outcome.Stars });
                }
            }
            else
            {
                SetStatus(
                    $"DEFEAT at turn {outcome?.TurnCount}/{outcome?.TurnLimit}"
                    + $" ({outcome?.FailReason ?? "unknown"}). Retry to sail again.");
            }

            SetOrderText(string.Empty);
            MissionFinished?.Invoke(_mission, outcome);
        }

        private string CompletionLineFor(Mission01TurnRecord record)
        {
            if (!_missionOver)
            {
                return $"Turn {record.Turn} complete — {record.Summary?.EnemyRemaining ?? 0} enemy ship(s) still afloat";
            }

            return _lastOutcome?.Result == "win"
                ? $"{_mission.Title.ToUpperInvariant()} WON at turn {_lastOutcome.TurnCount}"
                : $"MISSION LOST at turn {_lastOutcome?.TurnCount} ({_lastOutcome?.FailReason})";
        }

        private void BeginOrderEntry()
        {
            if (_authoredTurns.Count >= _mission.TurnLimit)
            {
                // Authoring the whole limit without a result is the timeout
                // loss; the last resolve already reported it.
                Finish();
                return;
            }

            // Scripted arrivals join before the turn they arrive on is
            // authored, so they can be targeted that very turn — the server
            // splices them in before resolving it.
            AddArrivals(TurnNumber);
            if (_mission.ArrivalsAt(TurnNumber).Count > 0)
            {
                ShowBoard($"Turn {TurnNumber}: enemy reinforcements have arrived!");
            }

            var living = _battle.LivingShips("player");
            _session = new PvpOrderSession(
                "Player",
                living,
                _battle.LivingShips("enemy"),
                $"Turn {TurnNumber}/{_mission.TurnLimit} — your orders:",
                allowChainShot: _mission.AllowsChainShot,
                allowBoarding: _mission.AllowsBoarding);
            Phase = PlayPhase.OrderEntry;
            SetStatus($"Turn {TurnNumber}/{_mission.TurnLimit} — enter orders for {living.Count} ship(s). {Hint()}");
            RefreshOrderText();
        }

        private string Hint()
        {
            if (_mission.AllowsChainShot)
            {
                return "Chain shot shreds rigging; round shot sinks hulls.";
            }

            return _mission.AllowsBoarding
                ? "Broadsides hit hardest from the beam; Board grapples a target instead of firing."
                : "Broadsides hit hardest from the beam; mind the wind.";
        }

        private string ControlsLine()
        {
            var builder = new StringBuilder("Next Ship | Turn +/- | Speed +/- | Target");
            if (_mission.AllowsChainShot)
            {
                builder.Append(" | Ammo");
            }
            if (_mission.AllowsBoarding)
            {
                builder.Append(" | Board");
            }

            builder.Append(" | Confirm Turn | Undo Turn");
            return builder.ToString();
        }

        private static string DescribeBonuses(PlayableMissionOutcome outcome)
        {
            if (outcome.Bonuses == null || outcome.Bonuses.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder("Bonuses: ");
            for (var i = 0; i < outcome.Bonuses.Count; i++)
            {
                var bonus = outcome.Bonuses[i];
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(bonus.Label).Append(bonus.Achieved ? " (yes)" : " (no)");
            }

            return builder.Append('.').ToString();
        }

        private void WithSession(Action<PvpOrderSession> mutate)
        {
            if (_session == null)
            {
                return;
            }

            mutate(_session);
            RefreshOrderText();
        }

        private void RefreshOrderText()
        {
            if (_session == null)
            {
                return;
            }

            if (orderPanel != null)
            {
                orderPanel.Render(_session);
                // The rows carry the orders; the label keeps only the
                // headline and hints.
                SetOrderText(_session.Headline + "\n" + ControlsLine());
            }
            else
            {
                SetOrderText(_session.Describe() + "\n" + ControlsLine());
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            Debug.Log($"[MissionPlay] {message}");
        }

        private void SetOrderText(string message)
        {
            if (orderPanel != null && _session == null)
            {
                orderPanel.Clear();
            }
            if (orderLabel != null)
            {
                orderLabel.text = message;
            }
        }
    }
}
