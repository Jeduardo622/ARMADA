using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    public sealed class UpgradePurchaseFlowResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; }
        public UpgradePurchaseResponse Purchase { get; init; }
    }

    /// <summary>
    /// Drives the upgrade purchase client flow: reads the catalog and the
    /// caller's owned tiers, then asks the server to purchase the next
    /// sequential tier for a component. The server stays authoritative over
    /// tier progression and costs; the client only verifies the response is
    /// the tier it expected.
    /// </summary>
    public sealed class UpgradesFlow
    {
        private readonly IUpgradesClient _client;

        public UpgradesFlow(IUpgradesClient client)
        {
            _client = client;
        }

        public async Task<UpgradePurchaseFlowResult> PurchaseNextTierAsync(string playerId, string component)
        {
            var list = await _client.GetUpgradesAsync();
            if (!list.Success || list.Data == null)
            {
                return Fail("list_failed");
            }

            var owned = list.Data.Owned?.Find(entry => entry.Component == component);
            if (owned == null)
            {
                return Fail("unknown_component");
            }
            var expectedTier = owned.Tier + 1;

            var purchase = await _client.PurchaseAsync(new UpgradePurchaseRequest
            {
                PlayerId = playerId,
                Component = component,
                Tier = expectedTier
            });
            if (!purchase.Success || purchase.Data == null)
            {
                return Fail("purchase_failed");
            }
            if (purchase.Data.Upgrade == null || purchase.Data.Upgrade.Component != component)
            {
                return Fail("component_mismatch");
            }
            if (purchase.Data.Upgrade.Tier != expectedTier)
            {
                return Fail("tier_mismatch");
            }

            return new UpgradePurchaseFlowResult { Success = true, Purchase = purchase.Data };
        }

        private static UpgradePurchaseFlowResult Fail(string reason)
        {
            return new UpgradePurchaseFlowResult { Success = false, FailureReason = reason };
        }
    }
}
