using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using Armada.Client.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Armada.Client.UI
{
    public sealed class InventoryUIController : MonoBehaviour
    {
        [SerializeField] private Text statusLabel;
        [SerializeField] private InventoryService inventoryService;
        [SerializeField] private AuthService authService;

        private async void Start()
        {
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            var player = authService.CurrentPlayer;
            if (player == null)
            {
                SetStatus("Player not authed.");
                return;
            }

            SetStatus("Loading inventory...");
            var result = await inventoryService.ListAsync(player.Id);
            if (!result.Success || result.FeatureDisabled)
            {
                SetStatus(FriendlyStatus(result.Status, result.ErrorReason, "Inventory unavailable."));
                return;
            }

            SetStatus($"Inventory items: {result.Data?.Count ?? 0}");
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
            Debug.Log($"[InventoryUI] {message}");
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

