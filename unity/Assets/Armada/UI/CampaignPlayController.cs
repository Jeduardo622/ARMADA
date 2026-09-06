using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>Connects normal UI orders to server resolution and the existing visual playback.</summary>
    public sealed class CampaignPlayController : MonoBehaviour
    {
        private CampaignUIController _view;
        private SpectatorRenderer _renderer;
        private Func<string> _playerId;
        private Action _harbor;
        private Action<int> _next;
        private CampaignBattleActions _actions;
        private bool _advancing;
        public CampaignBattleSession Session { get; private set; }
        public Task ActiveOperation { get; private set; } = Task.CompletedTask;

        public void Compose(CampaignUIController view, SpectatorRenderer renderer, CampaignBattleSession session,
            Func<string> playerId, Action harbor, Action<int> next)
        {
            _view = view; _renderer = renderer; Session = session;
            _playerId = playerId; _harbor = harbor; _next = next;
            _actions = new CampaignBattleActions
            {
                NextShip = OnNextShip, TurnLeft = OnTurnLeft, TurnRight = OnTurnRight,
                SlowDown = OnSlowDown, SpeedUp = OnSpeedUp, Target = OnTarget, Attack = OnAttack,
                Ammo = OnAmmo, Confirm = OnConfirm, Undo = OnUndo, Pause = OnPause,
                Faster = OnFaster, Harbor = OnHarbor
            };
        }

        public void BeginMission() { if (Session != null && !Session.IsBusy) ActiveOperation = BeginAsync(); }
        private async Task BeginAsync()
        {
            _view.ShowMessage("PREPARING TO SAIL", "Reading the wind and enemy formation…", null);
            await Session.BeginAsync();
            if (this == null) return;
            if (Session.Phase == CampaignPhase.Error)
            {
                _view.ShowMessage("UNABLE TO SAIL", "The battle could not be loaded. Check your connection and retry.", BeginMission, _harbor);
                return;
            }
            ShowBoard(); Render();
        }

        public void OnNextShip() => Edit(s => s.NextShip());
        public void OnTurnLeft() => Edit(s => s.AdjustTurn(-1));
        public void OnTurnRight() => Edit(s => s.AdjustTurn(1));
        public void OnSlowDown() => Edit(s => s.AdjustSpeed(-1));
        public void OnSpeedUp() => Edit(s => s.AdjustSpeed(1));
        public void OnTarget() => Edit(s => s.CycleTarget());
        public void OnAttack() => Edit(s => s.CycleAction());
        public void OnAmmo() => Edit(s => s.ToggleAmmo());
        private void Edit(Action<CampaignOrderSession> edit)
        {
            if (Session?.Phase != CampaignPhase.OrderEntry) return;
            edit(Session.Orders); Render();
        }
        public void OnUndo()
        {
            if (Session?.Phase != CampaignPhase.OrderEntry) return;
            Session.Undo(); ShowBoard(); Render();
        }
        public void OnConfirm()
        {
            if (Session?.Phase == CampaignPhase.OrderEntry) ActiveOperation = SubmitAsync();
        }
        private async Task SubmitAsync()
        {
            var task = Session.SubmitAsync();
            Render();
            await task;
            if (this == null) return;
            Render();
            if (Session.Phase != CampaignPhase.Playback) return;
            var record = Session.ActiveRecord;
            var state = record.StartState;
            if (_renderer != null)
            {
                _renderer.Resume();
                _renderer.BeginTurns(state.Ships, new List<Mission01TurnRecord> { record }, Session.Mission.TurnLimit,
                    $"Turn {record.Turn}: the fleet answers your orders.", "Turn complete.",
                    Session.OpeningState.Ships, state.Obstacles, state.SlowZones, state.Wind);
            }
            else await AdvanceAsync();
        }
        private void Update()
        {
            if (Session?.Phase != CampaignPhase.Playback || _advancing || _renderer == null) return;
            _view.UpdateBattleLog(_renderer.HudText);
            if (_renderer.IsFinished) ActiveOperation = AdvanceAsync();
        }
        private async Task AdvanceAsync()
        {
            if (_advancing) return;
            _advancing = true;
            try
            {
                Session.FinishPlayback();
                if (Session.Phase == CampaignPhase.Victory) await SaveAsync();
                else { if (Session.Phase == CampaignPhase.OrderEntry) ShowBoard(); Render(); }
            }
            finally { _advancing = false; }
        }
        public void OnRetrySave()
        {
            if (Session?.Phase == CampaignPhase.SaveFailed) ActiveOperation = SaveAsync();
        }
        private async Task SaveAsync()
        {
            var task = Session.SaveAsync(_playerId());
            Render();
            await task;
            if (this != null) Render();
        }
        public void OnPause()
        {
            if (Session?.Phase != CampaignPhase.Playback || _renderer == null) return;
            if (_renderer.IsPaused) _renderer.Resume(); else _renderer.Pause();
        }
        public void OnFaster() { if (Session?.Phase == CampaignPhase.Playback) _renderer?.CycleSpeedPreset(1); }
        public void OnHarbor()
        {
            if (Session == null || Session.IsBusy) return;
            if (Session.Phase == CampaignPhase.OrderEntry || Session.Phase == CampaignPhase.SaveFailed)
                _view.ConfirmLeave(Session.Phase == CampaignPhase.SaveFailed
                    ? "This victory has not been saved. Leaving will lose this battle and its unclaimed rewards. Previously saved progress is kept."
                    : "Your current battle will be lost. Saved campaign progress is kept.", Render, () => { enabled = false; _harbor(); });
            else { enabled = false; _harbor(); }
        }
        private void ShowBoard()
        {
            var s = Session.State;
            _renderer?.ShowBoard(s.Ships, "Awaiting your orders.", Session.OpeningState.Ships, s.Obstacles, s.SlowZones, s.Wind);
        }
        private void Render()
        {
            var phase = Session.Phase;
            if (phase == CampaignPhase.OrderEntry || phase == CampaignPhase.Resolving || phase == CampaignPhase.Playback)
            {
                _view.ShowBattle(Session.Mission, phase == CampaignPhase.OrderEntry ? Session.TurnNumber : Session.AuthoredTurns,
                    Session.Orders?.Describe() ?? "", SpectatorRenderer.ComposeConditions(Session.State?.Wind, Session.TurnNumber, Session.Mission.TurnLimit),
                    phase == CampaignPhase.OrderEntry, Session.AuthoredTurns > 0, _actions);
                if (Session.Error != null) _view.UpdateBattleLog("Orders were not resolved. Your choices are kept; confirm again to retry.");
                return;
            }
            var detail = phase == CampaignPhase.Saving ? "Saving your victory and rewards…"
                : phase == CampaignPhase.SaveFailed ? "Your victory has not been saved. Retry when your connection returns."
                : phase == CampaignPhase.Defeat ? "The fleet could not complete its objective. Try a new course or concentrate your fire."
                : $"Completed in {Session.Outcome?.TurnCount} turns. Your campaign progress is saved.";
            if (phase == CampaignPhase.Saved && Session.Outcome?.BonusObjectives != null)
                detail += "\n" + string.Join("  /  ", Session.Outcome.BonusObjectives.Select(p => $"{(p.Value ? "ACHIEVED" : "MISSED")}: {BonusName(p.Key)}"));
            if (phase == CampaignPhase.Saved)
            {
                var rewards = Session.SavedResult?.Data?.RewardsGranted;
                detail += rewards != null && rewards.Count > 0
                    ? "\nEarned: " + string.Join(", ", rewards.Select(r => $"{r.Quantity} {r.ItemKey.Replace('_', ' ')}"))
                    : "\nFirst-clear rewards already claimed.";
            }
            _view.ShowResults(Session.Mission, phase, Session.Stars, detail,
                phase == CampaignPhase.Defeat ? (Action)BeginMission : OnRetrySave,
                () => { enabled = false; _next(Session.Mission.Number + 1); }, OnHarbor);
        }
        private static string BonusName(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "underhulldamagethreshold": return "Low hull damage";
                case "withinturntarget": case "swiftvictory": return "Swift victory";
                case "heldweathergage": return "Weather gage";
                case "landedrakinghits": return "Raking hits";
                case "successfulboarding": return "Boarding action";
                case "noshiplost": return "Fleet survived";
                case "enemyignited": return "Enemy ignited";
                case "unscorched": return "Unscorched";
                case "cleantack": return "Clean tack";
                case "hullbreaker": return "Hull breaker";
                case "unrammed": return "Unrammed";
                case "sailshredder": return "Sail shredder";
                case "mixedbattery": return "Mixed battery";
                default: return System.Text.RegularExpressions.Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            }
        }
    }
}
