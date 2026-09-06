using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json;

namespace Armada.Client.Services
{
    public enum CampaignPhase { Idle, Loading, OrderEntry, Resolving, Playback, Victory, Defeat, Saving, Saved, SaveFailed, Error }

    /// <summary>Authored-turn state machine; future simulated turns are used only for their next planning snapshot.</summary>
    public sealed class CampaignBattleSession
    {
        private readonly CampaignMissionFlow _flow;
        private readonly List<List<SimOrder>> _turns = new();
        private readonly List<Mission01TurnRecord> _records = new();
        private readonly List<SimState> _planning = new();
        private List<CampaignOrderDraft> _previousDrafts;
        private SimState _next;
        private bool _ended;
        public CampaignMissionDefinition Mission { get; }
        public CampaignPhase Phase { get; private set; }
        public SimState State { get; private set; }
        public SimState OpeningState { get; private set; }
        public CampaignOrderSession Orders { get; private set; }
        public Mission01TurnRecord ActiveRecord { get; private set; }
        public CampaignOutcome Outcome { get; private set; }
        public CampaignSaveResult SavedResult { get; private set; }
        public string Error { get; private set; }
        public int TurnNumber => _turns.Count + 1;
        public int AuthoredTurns => _turns.Count;
        public bool IsBusy => Phase == CampaignPhase.Loading || Phase == CampaignPhase.Resolving || Phase == CampaignPhase.Playback || Phase == CampaignPhase.Saving;
        public int Stars => Outcome?.Result == "win" ? 1 + (Outcome.BonusObjectives?.Count(p => p.Value) ?? 0) : 0;

        public CampaignBattleSession(CampaignMissionDefinition mission, ICampaignMissionClient client)
        {
            Mission = mission;
            _flow = new CampaignMissionFlow(client, mission.Number, mission.Code, mission.Seed);
        }

        public async Task BeginAsync()
        {
            if (IsBusy) return;
            Phase = CampaignPhase.Loading;
            Error = null;
            _turns.Clear(); _records.Clear(); _planning.Clear();
            Outcome = null; SavedResult = null; ActiveRecord = null; Orders = null; _previousDrafts = null;
            var run = await _flow.ResolveAsync(_turns);
            if (!run.Success || run.Outcome?.Turns == null || run.Outcome.Turns.Count == 0 || run.Outcome.Turns[0].StartState?.Ships == null)
            {
                Error = run.Error ?? "missing_planning_snapshot";
                Phase = CampaignPhase.Error;
                return;
            }
            State = Copy(run.Outcome.Turns[0].StartState);
            OpeningState = Copy(State);
            _planning.Add(Copy(State));
            BeginOrders();
        }

        public async Task SubmitAsync()
        {
            if (Phase != CampaignPhase.OrderEntry || Orders == null) return;
            Error = null;
            _turns.Add(Orders.BuildOrders());
            _previousDrafts = Copy(Orders.Drafts.ToList());
            Phase = CampaignPhase.Resolving;
            var run = await _flow.ResolveAsync(_turns);
            var count = _turns.Count;
            var records = run.Outcome?.Turns;
            if (!run.Success || records == null || records.Count < count || records[count - 1].StartState?.Ships == null || records[count - 1].NextState?.Ships == null)
            {
                _turns.RemoveAt(count - 1);
                Error = run.Error ?? "missing_turn_snapshot";
                Phase = CampaignPhase.OrderEntry;
                return;
            }
            var ended = run.Outcome.TurnCount <= count;
            if (!ended && (records.Count <= count || records[count].StartState?.Ships == null))
            {
                _turns.RemoveAt(count - 1);
                Error = "missing_planning_snapshot";
                Phase = CampaignPhase.OrderEntry;
                return;
            }
            Outcome = run.Outcome;
            ActiveRecord = Copy(records[count - 1]);
            _records.Add(ActiveRecord);
            _ended = ended;
            // Never use the nextState of a future, unsubmitted turn.
            _next = Copy(ended ? ActiveRecord.NextState : records[count].StartState);
            Phase = CampaignPhase.Playback;
        }

        public void FinishPlayback()
        {
            if (Phase != CampaignPhase.Playback) return;
            State = _next;
            _planning.Add(Copy(State));
            if (_ended)
            {
                Orders = null;
                Phase = Outcome.Result == "win" ? CampaignPhase.Victory : CampaignPhase.Defeat;
            }
            else BeginOrders();
        }

        public async Task SaveAsync(string playerId)
        {
            if (Phase != CampaignPhase.Victory && Phase != CampaignPhase.SaveFailed) return;
            Phase = CampaignPhase.Saving;
            Error = null;
            SavedResult = await _flow.CompleteAsync(playerId);
            Phase = SavedResult.Success ? CampaignPhase.Saved : CampaignPhase.SaveFailed;
            Error = SavedResult.Success ? null : SavedResult.Error ?? "save_failed";
        }

        public void Undo()
        {
            if (Phase != CampaignPhase.OrderEntry || _turns.Count == 0) return;
            _turns.RemoveAt(_turns.Count - 1);
            _records.RemoveAt(_records.Count - 1);
            _planning.RemoveAt(_planning.Count - 1);
            State = Copy(_planning[_planning.Count - 1]);
            ActiveRecord = _records.Count == 0 ? null : _records[_records.Count - 1];
            Outcome = null;
            Error = null;
            BeginOrders();
        }

        private void BeginOrders()
        {
            Orders = new CampaignOrderSession(State, Mission.BoardingAllowed, Mission.ChainShotAllowed, _previousDrafts);
            Phase = CampaignPhase.OrderEntry;
        }
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }
}
