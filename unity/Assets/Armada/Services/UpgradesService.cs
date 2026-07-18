using System.Net;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public interface IUpgradesClient
    {
        Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync();
        Task<ServiceResult<UpgradePurchaseResponse>> PurchaseAsync(UpgradePurchaseRequest request);
    }

    public sealed class UpgradesService : IUpgradesClient
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private const string FeatureKey = "inventory";

        public UpgradesService(ApiClient client, FeatureFlags flags)
        {
            _client = client;
            _flags = flags;
        }

        public async Task<ServiceResult<UpgradesResponse>> GetUpgradesAsync()
        {
            var resp = await _client.SendAsync<UpgradesResponse>("/upgrades", UnityWebRequest.kHttpVerbGET);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<UpgradesResponse>.FromResponse(resp, featureDisabled);
        }

        public async Task<ServiceResult<UpgradePurchaseResponse>> PurchaseAsync(UpgradePurchaseRequest request)
        {
            var resp = await _client.SendAsync<UpgradePurchaseResponse>("/upgrades/purchase", UnityWebRequest.kHttpVerbPOST, request);
            var featureDisabled = false;
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                featureDisabled = true;
            }

            return ServiceResult<UpgradePurchaseResponse>.FromResponse(resp, featureDisabled);
        }
    }
}
