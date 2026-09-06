using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;
using Newtonsoft.Json;

namespace Armada.Client.Services
{
    public sealed class CampaignOutcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Dictionary<string, bool> BonusObjectives { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }
    public sealed class CampaignRunResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public CampaignOutcome Outcome { get; set; }
    }
    public sealed class CampaignSaveResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public MissionCompleteResponse Data { get; set; }
    }
    public interface ICampaignMissionClient
    {
        Task<CampaignRunResult> ResolveAsync(int missionNumber, int seed, List<List<SimOrder>> turns);
        Task<CampaignSaveResult> CompleteAsync(int missionNumber, string code, MissionCompleteRequest request);
    }
    public sealed class CampaignMissionFlow
    {
        private readonly ICampaignMissionClient _client;
        private readonly int _number;
        private readonly string _code;
        private readonly int _seed;
        private List<List<SimOrder>> _proof;
        private CampaignOutcome _outcome;
        private CampaignSaveResult _saved;
        private Task<CampaignSaveResult> _saving;
        private string _savingPlayer;
        private bool _resolving;

        public CampaignMissionFlow(ICampaignMissionClient client, int number, string code, int seed)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _number = number;
            _code = code;
            _seed = seed;
        }

        public async Task<CampaignRunResult> ResolveAsync(List<List<SimOrder>> turns)
        {
            if (_resolving || _saving != null) return new CampaignRunResult { Error = "busy" };
            _proof = null;
            _outcome = null;
            _saved = null;
            _resolving = true;
            try
            {
                var snapshot = Copy(turns ?? new List<List<SimOrder>>());
                var run = await _client.ResolveAsync(_number, _seed, Copy(snapshot));
                if (!run.Success || run.Outcome == null) return run;
                if (run.Outcome.MissionCode != _code || run.Outcome.Seed != _seed)
                    return new CampaignRunResult { Error = "outcome_mismatch" };
                _proof = snapshot;
                _outcome = Copy(run.Outcome);
                return run;
            }
            catch (Exception)
            {
                return new CampaignRunResult { Error = "connection_failed" };
            }
            finally { _resolving = false; }
        }

        public Task<CampaignSaveResult> CompleteAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return Failure("player_required");
            if (_saved != null) return _savingPlayer == playerId ? Task.FromResult(_saved) : Failure("session_changed");
            if (_saving != null) return _savingPlayer == playerId ? _saving : Failure("session_changed");
            // The resolver simulates idle future turns after the submitted prefix.
            // A forecast win is not a victory the player has actually authored.
            if (_resolving || _proof == null || _outcome?.Result != "win" ||
                _outcome.TurnCount < 1 || _outcome.TurnCount > _proof.Count) return Failure("no_authored_win");
            _savingPlayer = playerId;
            var task = SaveAsync(playerId);
            if (!task.IsCompleted) _saving = task;
            return task;
        }

        private async Task<CampaignSaveResult> SaveAsync(string playerId)
        {
            try
            {
                var result = new Dictionary<string, object> { ["outcome"] = "win" };
                var response = await _client.CompleteAsync(_number, _code, new MissionCompleteRequest
                {
                    PlayerId = playerId, Result = result, Seed = _seed, Turns = Copy(_proof)
                });
                if (response.Success) _saved = response;
                return response;
            }
            catch (Exception) { return new CampaignSaveResult { Error = "connection_failed" }; }
            finally { _saving = null; }
        }

        private static Task<CampaignSaveResult> Failure(string reason) => Task.FromResult(new CampaignSaveResult { Error = reason });
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }
}
