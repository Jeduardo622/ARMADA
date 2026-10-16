using System;
using System.Collections.Generic;
using System.Text;
using Armada.Client.Playback;
using Armada.Client.Services;
using TMPro;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// The campaign's front end: a mission-select menu over the ten missions,
    /// a briefing for the selected one, and the hand-off to
    /// <see cref="MissionPlayController"/> for the battle itself. When a run
    /// ends the player can retry it or return to the list, which shows the
    /// best star rating earned this session.
    ///
    /// Everything lives in one scene and switches panels rather than loading
    /// scenes, so the campaign needs no build-settings scene list (a protected
    /// project setting). Star ratings are session-local: the backend records
    /// completions but has no route that reads a player's progress back yet.
    ///
    /// Button handlers are public so the generated scene wires uGUI buttons
    /// to them; panels and labels are optional so tests can drive the
    /// controller with no UI at all.
    /// </summary>
    public sealed class CampaignController : MonoBehaviour
    {
        public enum CampaignPhase
        {
            Idle,
            MissionSelect,
            Briefing,
            InMission,
            Result
        }

        [Header("Panels (optional)")]
        [Tooltip("Mission list + briefing; shown outside battle.")]
        [SerializeField] private GameObject menuPanel;
        [Tooltip("Order-entry and playback strips; shown during battle.")]
        [SerializeField] private GameObject battleControls;
        [Tooltip("Retry / Missions strip; shown once a run has ended.")]
        [SerializeField] private GameObject resultControls;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text missionListLabel;
        [SerializeField] private TMP_Text briefingLabel;

        [Header("Play loop")]
        [SerializeField] private MissionPlayController playController;

        private readonly Dictionary<int, int> _bestStars = new();
        private Func<int, IPlayableMissionRunner> _runnerFactory;
        private SpectatorRenderer _spectator;
        private MissionUIController _missionUI;
        private PlayableMissionInfo _selected;
        private IPlayableMissionRunner _runner;
        private bool _subscribed;

        public CampaignPhase Phase { get; private set; } = CampaignPhase.Idle;

        public PlayableMissionInfo SelectedMission => _selected;

        /// <summary>Best stars earned this session for a mission (0 if never
        /// won); test hook and menu source.</summary>
        public int BestStars(int missionNumber)
        {
            return _bestStars.TryGetValue(missionNumber, out var stars) ? stars : 0;
        }

        public void Compose(
            Func<int, IPlayableMissionRunner> runnerFactory,
            SpectatorRenderer spectator,
            MissionUIController missionUI,
            MissionPlayController play = null)
        {
            _runnerFactory = runnerFactory;
            _spectator = spectator;
            _missionUI = missionUI;
            if (play != null)
            {
                playController = play;
            }

            if (playController != null && !_subscribed)
            {
                playController.MissionFinished += OnMissionFinished;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (playController != null && _subscribed)
            {
                playController.MissionFinished -= OnMissionFinished;
            }
        }

        /// <summary>Opens the mission list.</summary>
        public void ShowMissionSelect()
        {
            _selected = null;
            Phase = CampaignPhase.MissionSelect;
            ShowPanels(menu: true, battle: false, result: false);
            SetText(missionListLabel, DescribeMissionList());
            SetText(briefingLabel, "Choose a mission.");
            if (_spectator != null)
            {
                _spectator.ShowBoard(new List<Core.SimShip>(), "Armada — choose a mission to set sail.");
            }
        }

        // --- Button handlers (wired by CampaignSceneBuilder) ---

        /// <summary>Selects a mission and shows its briefing.</summary>
        public void OnSelectMission(int number)
        {
            if (Phase != CampaignPhase.MissionSelect && Phase != CampaignPhase.Briefing)
            {
                return;
            }

            var mission = PlayableMissionCatalog.ByNumber(number);
            if (mission == null)
            {
                return;
            }

            _selected = mission;
            Phase = CampaignPhase.Briefing;
            SetText(briefingLabel, DescribeBriefing(mission));
        }

        /// <summary>Starts the selected mission.</summary>
        public void OnSetSail()
        {
            if (Phase != CampaignPhase.Briefing || _selected == null)
            {
                return;
            }

            if (_runnerFactory == null || playController == null)
            {
                SetText(briefingLabel, "Campaign not composed.");
                return;
            }

            _runner = _runnerFactory(_selected.Number);
            if (_runner == null)
            {
                SetText(briefingLabel, $"{_selected.DisplayName} is not available.");
                return;
            }

            Phase = CampaignPhase.InMission;
            ShowPanels(menu: false, battle: true, result: false);
            playController.Compose(_selected, _runner, _spectator, _missionUI);
            playController.BeginMission();
        }

        /// <summary>Replays the mission that just ended.</summary>
        public void OnRetry()
        {
            if (Phase != CampaignPhase.Result || playController == null)
            {
                return;
            }

            Phase = CampaignPhase.InMission;
            ShowPanels(menu: false, battle: true, result: false);
            playController.OnRetryMission();
        }

        /// <summary>Returns to the mission list from a briefing or a result.</summary>
        public void OnReturnToMissions()
        {
            if (Phase == CampaignPhase.Briefing || Phase == CampaignPhase.Result)
            {
                ShowMissionSelect();
            }
        }

        private void OnMissionFinished(PlayableMissionInfo mission, PlayableMissionOutcome outcome)
        {
            if (Phase != CampaignPhase.InMission)
            {
                return;
            }

            var stars = outcome?.Stars ?? 0;
            if (mission != null && stars > BestStars(mission.Number))
            {
                _bestStars[mission.Number] = stars;
            }

            Phase = CampaignPhase.Result;
            ShowPanels(menu: false, battle: false, result: true);
        }

        private string DescribeMissionList()
        {
            var builder = new StringBuilder("CAMPAIGN");
            foreach (var mission in PlayableMissionCatalog.All)
            {
                builder.Append('\n').Append(mission.DisplayName).Append("  —  ");
                var stars = BestStars(mission.Number);
                builder.Append(stars > 0 ? $"{stars}/3 stars" : "not yet won");
            }

            return builder.ToString();
        }

        private string DescribeBriefing(PlayableMissionInfo mission)
        {
            var builder = new StringBuilder();
            builder.Append(mission.DisplayName).Append('\n').Append(mission.Briefing);
            builder.Append("\nTurn limit ").Append(mission.TurnLimit);
            if (mission.AllowsBoarding)
            {
                builder.Append(" · boarding allowed");
            }
            if (mission.AllowsChainShot)
            {
                builder.Append(" · chain shot available");
            }

            var stars = BestStars(mission.Number);
            if (stars > 0)
            {
                builder.Append("\nBest this session: ").Append(stars).Append("/3 stars");
            }

            builder.Append("\nPress Set Sail to begin.");
            return builder.ToString();
        }

        private void ShowPanels(bool menu, bool battle, bool result)
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(menu);
            }
            if (battleControls != null)
            {
                battleControls.SetActive(battle);
            }
            if (resultControls != null)
            {
                resultControls.SetActive(result);
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }

            Debug.Log($"[Campaign] {text}");
        }
    }
}
