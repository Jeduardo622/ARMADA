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
    /// Networked PvP match UI: create or join by code, author ONLY this
    /// side's orders per turn, poll while waiting for the opponent (join or
    /// orders), and play each server-resolved turn back through the
    /// spectator renderer. All state lives in plain-C# collaborators
    /// (PvpNetplayFlow / PvpOrderSession); TMP labels are optional and
    /// null-guarded so tests never touch TMP.
    /// </summary>
    public sealed class PvpNetplayUIController : MonoBehaviour
    {
        public enum NetplayPhase
        {
            Menu,
            Connecting,
            WaitingForOpponentJoin,
            OrderEntry,
            WaitingForResolution,
            Playback,
            Finished,
            Error
        }

        [Header("UI Wiring (optional)")]
        [SerializeField] private TMP_Text orderLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Polling (design-tunable placeholder)")]
        [SerializeField] private float pollIntervalSeconds = 2f;

        private PvpNetplayFlow _flow;
        private SpectatorRenderer _spectator;
        private PvpOrderSession _session;
        private string _joinCode = string.Empty;
        private float _pollDueIn;
        private bool _pollInFlight;
        private bool _submitInFlight;

        public NetplayPhase Phase { get; private set; } = NetplayPhase.Menu;

        /// <summary>Order session for this side; test hook.</summary>
        public PvpOrderSession CurrentSession => _session;

        public string LastError { get; private set; }

        /// <summary>In-flight create/join/submit task; test hook.</summary>
        public Task ActiveRequest { get; private set; }

        public void Compose(PvpNetplayFlow flow, SpectatorRenderer spectator)
        {
            _flow = flow;
            _spectator = spectator;
        }

        public void ShowMenu()
        {
            Phase = NetplayPhase.Menu;
            SetStatus("PvP netplay: Create a match, or enter a code and Join.");
            SetOrderText(string.Empty);
        }

        // --- Handlers wired by PvPNetplayDemoSceneBuilder ---

        public void SetJoinCode(string code)
        {
            _joinCode = code ?? string.Empty;
        }

        public void OnCreateMatch()
        {
            if (Phase != NetplayPhase.Menu || _flow == null)
            {
                return;
            }

            Phase = NetplayPhase.Connecting;
            SetStatus("Creating match...");
            ActiveRequest = CreateAsync();
        }

        public void OnJoinMatch()
        {
            if (Phase != NetplayPhase.Menu || _flow == null || string.IsNullOrWhiteSpace(_joinCode))
            {
                return;
            }

            Phase = NetplayPhase.Connecting;
            SetStatus($"Joining match {_joinCode.Trim().ToUpperInvariant()}...");
            ActiveRequest = JoinAsync();
        }

        public void OnTurnLeft() => WithSession(session => session.AdjustTurn(-1));

        public void OnTurnRight() => WithSession(session => session.AdjustTurn(1));

        public void OnSpeedUp() => WithSession(session => session.AdjustSpeed(1));

        public void OnSpeedDown() => WithSession(session => session.AdjustSpeed(-1));

        public void OnCycleTarget() => WithSession(session => session.CycleTarget());

        public void OnToggleAmmo() => WithSession(session => session.ToggleAmmo());

        public void OnNextShip() => WithSession(session => session.NextShip());

        public void OnConfirmOrders()
        {
            if (Phase != NetplayPhase.OrderEntry || _session == null)
            {
                return;
            }

            var orders = _session.BuildOrders();
            _session = null;
            Phase = NetplayPhase.WaitingForResolution;
            // A full interval before the first poll: polling while the
            // submission is in flight could let a stale pre-resolution view
            // race the response (the in-flight gate below is the backstop).
            _pollDueIn = pollIntervalSeconds;
            SetStatus($"Turn {_flow.View.TurnNumber}: orders away — waiting for the enemy captain...");
            SetOrderText(string.Empty);
            ActiveRequest = SubmitAsync(orders);
        }

        /// <summary>
        /// Frame driver: waiting phases poll the server on an interval, the
        /// playback phase hands control back once the spectator finishes.
        /// Public so inactive-object tests can drive it manually (dt in
        /// seconds); Update forwards real frame time.
        /// </summary>
        public void Advance(float dt)
        {
            switch (Phase)
            {
                case NetplayPhase.WaitingForOpponentJoin:
                case NetplayPhase.WaitingForResolution:
                    _pollDueIn -= dt;
                    if (_pollDueIn <= 0f && !_pollInFlight && !_submitInFlight)
                    {
                        _pollDueIn = pollIntervalSeconds;
                        ActiveRequest = PollAsync();
                    }
                    break;
                case NetplayPhase.Playback:
                    if (_spectator == null || _spectator.IsFinished)
                    {
                        AfterPlayback();
                    }
                    break;
            }
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private async Task CreateAsync()
        {
            var result = await _flow.CreateAsync();
            if (!result.Success || _flow.View == null)
            {
                Fail($"create_failed:{result.ErrorReason ?? result.Status.ToString()}");
                return;
            }

            Phase = NetplayPhase.WaitingForOpponentJoin;
            _pollDueIn = pollIntervalSeconds;
            SetStatus($"Match code: {_flow.View.Code} — share it with your opponent. Waiting for them to join...");
        }

        private async Task JoinAsync()
        {
            var result = await _flow.JoinAsync(_joinCode);
            if (!result.Success || _flow.View == null)
            {
                Fail($"join_failed:{result.ErrorReason ?? result.Status.ToString()}");
                return;
            }

            BeginOrderEntry();
        }

        private async Task SubmitAsync(List<SimOrder> orders)
        {
            _submitInFlight = true;
            ServiceResult<PvpSubmitOrdersResponse> result;
            try
            {
                result = await _flow.SubmitOrdersAsync(orders);
            }
            finally
            {
                _submitInFlight = false;
            }

            if (!result.Success)
            {
                // A failed response is ambiguous: the server may have
                // committed the submission (or even resolved the turn)
                // before the transport dropped. Stay in the waiting phase
                // and reconcile against the participant view instead of
                // declaring a terminal error — the next poll either finds a
                // resolved turn, finds youSubmitted, or reopens order entry.
                SetStatus($"Order submission uncertain ({result.Status}) — checking with the server...");
                _pollDueIn = 0f;
                return;
            }

            if (result.Data.Resolved)
            {
                BeginPlaybackIfTurnReady();
            }
            else
            {
                _pollDueIn = pollIntervalSeconds;
            }
        }

        private async Task PollAsync()
        {
            _pollInFlight = true;
            try
            {
                var result = await _flow.PollAsync();
                if (!result.Success)
                {
                    // Polling failures are transient by nature; keep waiting
                    // and let the next interval retry.
                    SetStatus($"Connection hiccup ({result.Status}); retrying...");
                    return;
                }

                // An abandoned match expires server-side; stop waiting on it
                // in every polling phase instead of spinning forever.
                if (_flow.View?.Status == "EXPIRED")
                {
                    FinishExpired();
                    return;
                }

                if (Phase == NetplayPhase.WaitingForOpponentJoin)
                {
                    if (_flow.View.Status == "IN_PROGRESS" || _flow.View.OpponentJoined)
                    {
                        BeginOrderEntry();
                    }
                    return;
                }

                if (Phase == NetplayPhase.WaitingForResolution)
                {
                    if (BeginPlaybackIfTurnReady())
                    {
                        return;
                    }

                    // Reconciliation after an ambiguous submit failure: no
                    // resolved turn and the server has no staged orders from
                    // us, so the submission never landed — re-author it.
                    if (_flow.View?.YouSubmitted == false)
                    {
                        SetStatus("Submission was not received — please re-enter orders.");
                        BeginOrderEntry();
                    }
                }
            }
            finally
            {
                _pollInFlight = false;
            }
        }

        private bool BeginPlaybackIfTurnReady()
        {
            if (!_flow.TryDequeuePlaybackTurn(out var playback))
            {
                return false;
            }

            if (_spectator != null && playback.ShipsAtTurnStart != null)
            {
                _spectator.BeginTurns(
                    playback.ShipsAtTurnStart,
                    new List<Mission01TurnRecord> { playback.Record },
                    _flow.View.TurnLimit,
                    $"Turn {playback.Record.Turn}: broadsides fly...",
                    CompletionLine(playback.Record.Turn),
                    // Battle-start stats keep the HP/sail bars on true maxima
                    // when replaying a mid-battle turn snapshot.
                    PvpScenario.BuildInitialState().Ships);
                Phase = NetplayPhase.Playback;
                SetStatus($"Turn {playback.Record.Turn} resolved — spectating playback.");
            }
            else
            {
                AfterPlayback();
            }

            return true;
        }

        private void AfterPlayback()
        {
            var view = _flow.View;
            if (view.Status == "COMPLETED")
            {
                Phase = NetplayPhase.Finished;
                SetStatus($"{Verdict(view)} — match over after {view.Turns?.Count ?? 0} turns.");
                SetOrderText(string.Empty);
                return;
            }

            if (view.Status == "EXPIRED")
            {
                FinishExpired();
                return;
            }

            BeginOrderEntry();
        }

        private void FinishExpired()
        {
            Phase = NetplayPhase.Finished;
            SetStatus("MATCH EXPIRED — abandoned. Restart the scene to create or join a new match.");
            SetOrderText(string.Empty);
        }

        private void BeginOrderEntry()
        {
            _session = new PvpOrderSession(
                _flow.YourSide == "side_b" ? "B" : "A",
                _flow.OwnLivingShips(),
                _flow.EnemyLivingShips());
            Phase = NetplayPhase.OrderEntry;
            SetStatus($"Turn {_flow.View.TurnNumber} — you are side {(_flow.YourSide == "side_b" ? "B" : "A")}. Enter orders.");
            RefreshOrderText();
        }

        private string Verdict(PvpMatchView view)
        {
            var yourSideWon =
                (view.Result == "side_a" && view.YourSide == "side_a")
                || (view.Result == "side_b" && view.YourSide == "side_b");
            if (view.Result == "draw")
            {
                return "DRAW";
            }

            return yourSideWon ? "VICTORY — your side wins" : "DEFEAT — the enemy side wins";
        }

        private string CompletionLine(int turn)
        {
            var view = _flow.View;
            if (view.Status == "COMPLETED")
            {
                return $"{Verdict(view)} at turn {turn}";
            }

            return $"Turn {turn} complete";
        }

        private void Fail(string reason)
        {
            LastError = reason;
            Phase = NetplayPhase.Error;
            SetStatus($"Error: {reason}. Restart the scene to try again.");
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
                SetOrderText(_session.Describe() + "\nNext Ship | Turn +/- | Speed +/- | Target | Ammo | Confirm Orders");
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            Debug.Log($"[PvpNetplay] {message}");
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
