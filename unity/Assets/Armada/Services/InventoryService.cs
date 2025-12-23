using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class InventoryService
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private const string FeatureKey = "inventory";

        public InventoryService(ApiClient client, FeatureFlags flags)
        {
            _client = client;
            _flags = flags;
        }

        public async Task<ServiceResult<List<InventoryItem>>> ListAsync(string playerId)
        {
            var resp = await _client.SendAsync<InventoryResponse>($"/inventory/{playerId}", UnityWebRequest.kHttpVerbGET);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return new ServiceResult<List<InventoryItem>>
            {
                Data = resp.Data?.Items,
                Success = resp.Success,
                Status = resp.StatusCode,
                ErrorReason = resp.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }
    }
}

