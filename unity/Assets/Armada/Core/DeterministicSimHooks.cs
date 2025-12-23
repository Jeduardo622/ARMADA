using UnityEngine;

namespace Armada.Client.Core
{
    public sealed class DeterministicSimHooks : MonoBehaviour
    {
        [SerializeField] private ArmadaClientConfig config;
        [SerializeField] private int seedOverride = -1;

        private void Awake()
        {
            ApplyFixedTimestep();
            ApplySeed();
        }

        public void ApplyFixedTimestep()
        {
            if (config != null)
            {
                Time.fixedDeltaTime = config.FixedDeltaTime;
            }
        }

        public void ApplySeed(int? seed = null)
        {
            var chosen = seed ?? (seedOverride >= 0 ? seedOverride : config != null ? config.DefaultRandomSeed : 12345);
            Random.InitState(chosen);
            Debug.Log($"[Determinism] Random seed set to {chosen}");
        }
    }
}

