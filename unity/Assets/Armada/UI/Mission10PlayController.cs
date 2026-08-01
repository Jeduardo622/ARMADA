using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using TMPro;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Playable Mission 10 "Sail-Cutter" loop: the player authors orders for
    /// the surviving sloops one turn at a time, the turn resolves
    /// server-side, and the spectator renderer plays back just that turn
    /// before the next order round.
    ///
    /// The client never simulates. Each confirmed turn is appended to a
    /// client-held order array and the whole array is re-sent to
    /// /missions/mission-10-sail-cutter/resolve, which accepts a partial
    /// turns list. runMissionLoop feeds turn N only the previous turn's state
    /// and playerTurnOrders[N-1], so resolving the prefix [t1..tN] returns
    /// byte-identical records for turns 1..N no matter what follows — pinned
    /// by "mission 10 prefix stability" in tests/mission10.test.ts. Only the
    /// newest record is rendered; the tail the server simulates past the
    /// authored prefix (idle sloops) is ignored.
    ///
    /// Because the accumulated array is exactly what produced the win, it is
    /// also the completion proof: the winning turn's Mission10Flow.RunAsync
    /// snapshots it, and CompleteMission10 re-sends that snapshot.
    ///
    /// Button handlers are public so the generated scene wires uGUI buttons
    /// to them; all order state lives in plain-C# collaborators
    /// (PvpOrderSession / Mission10BattleState) and the TMP labels are
    /// optional so tests never touch TMP components.
    /// </summary>
    public sealed class Mission10PlayController : MonoBehaviour
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
        [SerializeField] private TMP_Text statusLabel;

        private Mission10Flow _flow;
        private SpectatorRenderer _spectator;
        private MissionUIController _missionUI;
        private int _seed;

        private readonly List<List<SimOrder>> _authoredTurns = new();
        private readonly List<Mission01TurnRecord> _resolvedRecords = new();
        private Mission10BattleState _battle;
        private PvpOrderSession _session;
        private Mission10Outcome _lastOutcome;
        private bool _missionOver;

        public PlayPhase Phase { get; private set; } = PlayPhase.Idle;

        /// <summary>Order session for the turn being authored; test hook.</summary>
        public PvpOrderSession CurrentSession => _session;

        /// <summary>1-based number of the turn being authored; test hook.</summary>
        public int TurnNumber => _authoredTurns.Count + 1;

        /// <summary>Outcome of the last resolve; test hook.</summary>
        public Mission10Outcome LastOutcome => _lastOutcome;

        public string LastError { get; private set; }

        /// <summary>In-flight turn submission; test hook.</summary>
        public Task ActiveSubmit { get; private set; }

        public void Compose(Mission10Flow flow, SpectatorRenderer spectator, MissionUIController missionUI, int seed)
        {
            _flow = flow;
            _spectator = spectator;
            _missionUI = missionUI;
            _seed = seed;
        }

        public void BeginMission()
        {
            if (_flow == null)
            {
                SetStatus("Mission 10 flow not composed.");
                return;
            }

            _authoredTurns.Clear();
            _resolvedRecords.Clear();
            _lastOutcome = null;
            _missionOver = false;
            LastError = null;
            _battle = new Mission10BattleState(Mission10Scenario.BuildExpectedStart(_seed).State.Ships);
            // Show the opening position before the first orders are written:
            // the player is aiming at this board, so it must be on screen.
            ShowBoard($"Mission 10 Sail-Cutter (seed {_seed}) — the clipper line stands off to the east.");
            BeginOrderEntry();
        }

        // --- Button handlers (wired by Mission10PlayDemoSceneBuilder) ---

        public void OnTurnLeft() => WithSession(session => session.AdjustTurn(-1));

        public void OnTurnRight() => WithSession(session => session.AdjustTurn(1));

        public void OnSpeedUp() => WithSession(session => session.AdjustSpeed(1));

        public void OnSpeedDown() => WithSession(session => session.AdjustSpeed(-1));

        public void OnCycleTarget() => WithSession(session => session.CycleTarget());

        public void OnToggleAmmo() => WithSession(session => session.ToggleAmmo());

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

            _battle = new Mission10BattleState(Mission10Scenario.BuildExpectedStart(_seed).State.Ships);
            foreach (var record in _resolvedRecords)
            {
                _battle.Apply(record);
            }

            // The renderer still shows the withdrawn turn's end positions and
            // damage; rewind it too, or the replacement orders are written
            // against a board a turn ahead of the retained prefix.
            ShowBoard(_resolvedRecords.Count == 0
                ? $"Turn 1 orders withdrawn — board rewound to the opening position."
                : $"Turn {_authoredTurns.Count + 1} orders withdrawn — board rewound to the end of turn {_resolvedRecords.Count}.");

            SetStatus($"Turn {_authoredTurns.Count + 1} orders withdrawn — re-enter them.");
            BeginOrderEntry();
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
                    Mission10Scenario.BuildExpectedStart(_seed).State.Ships,
                    wind: Mission10Scenario.BuildExpectedStart(_seed).State.Wind);
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

        private async Task ResolveTurnAsync()
        {
            var authoredCount = _authoredTurns.Count;
            var run = await _flow.RunAsync(_seed, _authoredTurns);
            if (!run.Success || run.Outcome?.Turns == null)
            {
                LastError = run.FailureReason ?? "resolve_failed";
                Phase = PlayPhase.Finished;
                SetStatus($"Turn failed: {LastError}");
                return;
            }

            var outcome = run.Outcome;
            if (outcome.Turns.Count < authoredCount)
            {
                // The prefix must produce a record per authored turn; fewer
                // means the contract this loop rests on has changed.
                LastError = "missing_turn_record";
                Phase = PlayPhase.Finished;
                SetStatus($"Turn failed: {LastError}");
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
                    Mission10Scenario.TurnLimit,
                    $"Turn {record.Turn}/{Mission10Scenario.TurnLimit}: broadsides fly...",
                    CompletionLineFor(record, authoredCount),
                    // Battle-start stats keep the HP/sail bars on the true
                    // maxima when replaying a mid-battle turn snapshot.
                    Mission10Scenario.BuildExpectedStart(_seed).State.Ships,
                    wind: Mission10Scenario.BuildExpectedStart(_seed).State.Wind);
                Phase = PlayPhase.Playback;
                SetStatus($"Turn {record.Turn} resolved — watching playback.");
            }
            else
            {
                Phase = PlayPhase.Playback;
                AdvanceAfterTurn();
            }
        }

        private void AdvanceAfterTurn()
        {
            if (!_missionOver)
            {
                BeginOrderEntry();
                return;
            }

            Phase = PlayPhase.Finished;
            var outcome = _lastOutcome;
            if (outcome?.Result == "win")
            {
                var bonuses = outcome.BonusObjectives;
                SetStatus(
                    $"VICTORY at turn {outcome.TurnCount}/{outcome.TurnLimit}"
                    + $" — bonuses: sailShredder={(bonuses?.SailShredder == true ? "yes" : "no")},"
                    + $" mixedBattery={(bonuses?.MixedBattery == true ? "yes" : "no")}. Saving...");
                // Completion goes through the flow-aware path so the proof
                // re-sends the exact seed and turns this run was resolved
                // with — the array the player authored.
                if (_missionUI != null)
                {
                    _missionUI.CompleteMission10(_flow, new Dictionary<string, object> { ["outcome"] = "win" });
                }
            }
            else
            {
                SetStatus(
                    $"DEFEAT at turn {outcome?.TurnCount}/{outcome?.TurnLimit}"
                    + $" ({outcome?.FailReason ?? "unknown"}). Restart the scene to sail again.");
            }

            SetOrderText(string.Empty);
        }

        private string CompletionLineFor(Mission01TurnRecord record, int authoredCount)
        {
            if (!_missionOver)
            {
                return $"Turn {record.Turn} complete — {record.Summary?.EnemyRemaining ?? 0} clipper(s) still afloat";
            }

            return _lastOutcome?.Result == "win"
                ? $"SAIL-CUTTER WON at turn {_lastOutcome.TurnCount}"
                : $"MISSION LOST at turn {_lastOutcome?.TurnCount} ({_lastOutcome?.FailReason})";
        }

        private void BeginOrderEntry()
        {
            if (_authoredTurns.Count >= Mission10Scenario.TurnLimit)
            {
                // Authoring the whole limit without a result is the timeout
                // loss; the last resolve already reported it.
                Phase = PlayPhase.Finished;
                return;
            }

            var living = _battle.LivingShips("player");
            _session = new PvpOrderSession(
                "Player",
                living,
                _battle.LivingShips("enemy"),
                $"Turn {TurnNumber}/{Mission10Scenario.TurnLimit} — your orders:");
            Phase = PlayPhase.OrderEntry;
            SetStatus(
                $"Turn {TurnNumber}/{Mission10Scenario.TurnLimit} — enter orders for {living.Count} sloop(s)."
                + " Chain shot shreds rigging; round shot sinks hulls.");
            RefreshOrderText();
        }

        private void WithSession(System.Action<PvpOrderSession> mutate)
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
            if (_session != null)
            {
                SetOrderText(_session.Describe()
                    + "\nNext Ship | Turn +/- | Speed +/- | Target | Ammo | Confirm Turn | Undo Turn");
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            Debug.Log($"[Mission10Play] {message}");
        }

        private void SetOrderText(string message)
        {
            if (orderLabel != null)
            {
                orderLabel.text = message;
            }
        }
    }
}
