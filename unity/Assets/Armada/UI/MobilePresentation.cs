using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// Mobile presentation defaults (GDD p.24; decision D2-B in
    /// docs/design/art-direction.md §3.3): caps the frame rate at 30 on
    /// handheld platforms for the battery/thermal budget in
    /// docs/perf-budgets.md. Desktop and the Editor keep the platform
    /// default, so the dev harness is unaffected.
    /// </summary>
    public sealed class MobilePresentation : MonoBehaviour
    {
        public const int MobileTargetFrameRate = 30;

        private void Awake()
        {
            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = MobileTargetFrameRate;
            }
        }
    }
}
