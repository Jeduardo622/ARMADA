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
    /// Hot-seat order-authoring UI for the pinned PvP 2v2 skirmish: side A
    /// enters orders, side B enters orders, the combined turn resolves
    /// server-side, and the spectator renderer plays it back before the next
    /// entry round. Button handlers are public so the generated scene wires
    /// uGUI buttons to them; all state lives in plain-C# collaborators
    /// (PvpOrderSession / PvpHotseatFlow) and the TMP labels are optional so
    /// tests never touch TMP components.
    /// </summary>
    public sealed class PvpHotseatUIController : MonoBehaviour
    {
        public enum HotseatPhase
        {
            Idle,
            SideAEntry,
            SideBEntry,
            Resolving,
            Playback,
            Finished
        }

        [Header("UI Wiring (optional)")]
        [SerializeField] private TMP_Text orderLabel;
        [SerializeField] private TMP_Text statusLabel;

        private PvpHotseatFlow _flow;
        private SpectatorRenderer _spectator;
        private PvpOrderSession _session;
        private List<SimOrder> _sideAOrders;

        public HotseatPhase Phase { get; private set; } = HotseatPhase.Idle;

        /// <summary>Order session for the side currently entering; test hook.</summary>
        public PvpOrderSession CurrentSession => _session;

        public string LastError { get; private set; }

        /// <summary>In-flight turn submission; test hook.</summary>
        public Task ActiveSubmit { get; private set; }

        public void Compose(PvpHotseatFlow flow, SpectatorRenderer spectator)
        {
            _flow = flow;
            _spectator = spectator;
        }

        public void BeginMatch()
        {
            if (_flow == null)
            {
                SetStatus("PvP flow not composed.");
                return;
            }

            BeginSideEntry(HotseatPhase.SideAEntry);
        }

        // --- Button handlers (wired by PvPHotseatDemoSceneBuilder) ---

        public void OnTurnLeft() => WithSession(session => session.AdjustTurn(-1));

        public void OnTurnRight() => WithSession(session => session.AdjustTurn(1));

        public void OnSpeedUp() => WithSession(session => session.AdjustSpeed(1));

        public void OnSpeedDown() => WithSession(session => session.AdjustSpeed(-1));

        public void OnCycleTarget() => WithSession(session => session.CycleTarget());

        public void OnToggleAmmo() => WithSession(session => session.ToggleAmmo());

        public void OnNextShip() => WithSession(session => session.NextShip());

        public void OnConfirmSide()
        {
            if (_session == null)
            {
                return;
            }

            if (Phase == HotseatPhase.SideAEntry)
            {
                _sideAOrders = _session.BuildOrders();
                BeginSideEntry(HotseatPhase.SideBEntry);
            }
            else if (Phase == HotseatPhase.SideBEntry)
            {
                var sideBOrders = _session.BuildOrders();
                _session = null;
                Phase = HotseatPhase.Resolving;
                SetStatus($"Resolving turn {_flow.TurnNumber}...");
                SetOrderText(string.Empty);
                ActiveSubmit = ResolveTurnAsync(_sideAOrders, sideBOrders);
            }
        }

        /// <summary>
        /// Advances out of the playback phase once the spectator finishes.
        /// Called every frame from Update; public so inactive-object tests
        /// can drive the transition manually.
        /// </summary>
        public void PollPlayback()
        {
            if (Phase != HotseatPhase.Playback)
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

        private async Task ResolveTurnAsync(List<SimOrder> sideAOrders, List<SimOrder> sideBOrders)
        {
            var resolution = await _flow.SubmitTurnAsync(sideAOrders, sideBOrders);
            if (!resolution.Success)
            {
                LastError = resolution.FailureReason;
                Phase = HotseatPhase.Finished;
                SetStatus($"Turn failed: {resolution.FailureReason}");
                return;
            }

            if (_spectator != null)
            {
                _spectator.BeginTurns(
                    resolution.ShipsAtTurnStart,
                    new List<Mission01TurnRecord> { resolution.Record },
                    PvpScenario.TurnLimit,
                    $"Turn {resolution.Turn}: broadsides fly...",
                    CompletionLineFor(resolution));
                Phase = HotseatPhase.Playback;
                SetStatus($"Turn {resolution.Turn} resolved — spectating playback.");
            }
            else
            {
                Phase = HotseatPhase.Playback;
                AdvanceAfterTurn();
            }
        }

        private void AdvanceAfterTurn()
        {
            switch (_flow.MatchResult)
            {
                case PvpHotseatFlow.ResultOngoing:
                    BeginSideEntry(HotseatPhase.SideAEntry);
                    break;
                case PvpHotseatFlow.ResultSideA:
                    FinishMatch("SIDE A WINS");
                    break;
                case PvpHotseatFlow.ResultSideB:
                    FinishMatch("SIDE B WINS");
                    break;
                default:
                    FinishMatch("DRAW");
                    break;
            }
        }

        private void FinishMatch(string headline)
        {
            Phase = HotseatPhase.Finished;
            SetStatus($"{headline} — match over after turn {_flow.TurnNumber - 1}.");
            SetOrderText(string.Empty);
        }

        private string CompletionLineFor(PvpTurnResolution resolution)
        {
            switch (resolution.MatchResult)
            {
                case PvpHotseatFlow.ResultSideA:
                    return $"SIDE A WINS at turn {resolution.Turn}";
                case PvpHotseatFlow.ResultSideB:
                    return $"SIDE B WINS at turn {resolution.Turn}";
                case PvpHotseatFlow.ResultDraw:
                    return $"DRAW at turn {resolution.Turn}";
                default:
                    return $"Turn {resolution.Turn} complete";
            }
        }

        private void BeginSideEntry(HotseatPhase entryPhase)
        {
            var isSideA = entryPhase == HotseatPhase.SideAEntry;
            var ownSide = isSideA ? "player" : "enemy";
            var enemySide = isSideA ? "enemy" : "player";
            _session = new PvpOrderSession(
                isSideA ? "A" : "B",
                _flow.LivingShips(ownSide),
                _flow.LivingShips(enemySide));
            Phase = entryPhase;
            SetStatus(
                $"Turn {_flow.TurnNumber} — Side {(isSideA ? "A" : "B")} enter orders"
                + $" (hot-seat: other captain look away).");
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
                SetOrderText(_session.Describe() + "\nNext Ship | Turn +/- | Speed +/- | Target | Ammo | Confirm Side");
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            Debug.Log($"[PvpHotseat] {message}");
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
