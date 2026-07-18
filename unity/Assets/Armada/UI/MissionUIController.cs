using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using UnityEngine;
using TMPro;

namespace Armada.Client.UI
{
    public sealed class MissionUIController : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private MissionService missionService;
        [SerializeField] private AuthService authService;

        private async void Start()
        {
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            SetStatus("Loading missions...");
            var result = await missionService.ListAsync();
            if (!result.Success || result.FeatureDisabled)
            {
                SetStatus(FriendlyStatus(result.Status, result.ErrorReason, "Missions unavailable"));
                return;
            }

            SetStatus($"Loaded {result.Data?.Count ?? 0} missions");
        }

        public async void CompleteMission(string code, Dictionary<string, object> result, int? bestScore = null)
        {
            var player = authService.CurrentPlayer;
            if (player == null)
            {
                SetStatus("Player not authed.");
                return;
            }

            var progressResult = await missionService.CompleteAsync(code, new MissionCompleteRequest
            {
                PlayerId = player.Id,
                Result = result,
                BestScore = bestScore
            });

            if (!progressResult.Success || progressResult.FeatureDisabled)
            {
                SetStatus(FriendlyStatus(progressResult.Status, progressResult.ErrorReason, "Complete failed or feature off."));
            }
            else
            {
                var rewardCount = progressResult.Data?.RewardsGranted?.Count ?? 0;
                SetStatus($"Mission {code} saved (status {progressResult.Data?.Progress?.Status}, rewards {rewardCount})");
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
            Debug.Log($"[MissionsUI] {message}");
        }

        private static string FriendlyStatus(HttpStatusCode status, string reason, string fallback)
        {
            if (reason == "offline")
            {
                return "Offline. Check connection.";
            }

            return status switch
            {
                HttpStatusCode.Unauthorized => "Session expired. Please re-auth.",
                HttpStatusCode.Forbidden => "Feature disabled by server.",
                (HttpStatusCode)429 => "Rate limited. Please retry shortly.",
                _ => fallback
            };
        }
    }
}

