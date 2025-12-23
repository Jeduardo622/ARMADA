using System;
using System.Collections.Generic;
using UnityEngine;

namespace Armada.Client.Core
{
    [CreateAssetMenu(fileName = "ArmadaClientConfig", menuName = "Armada/Client Config")]
    public sealed class ArmadaClientConfig : ScriptableObject
    {
        [Header("Endpoints")]
        [SerializeField] private string baseUrl = "http://localhost:4500";
        [SerializeField] private string configSigningKey = "changeme";

        [Header("Features")]
        [SerializeField] private List<FeatureToggleSetting> featureToggles = new();

        [Header("Telemetry")]
        [SerializeField] private float telemetryFlushSeconds = 5f;
        [SerializeField] private int telemetryMaxBatchSize = 25;
        [SerializeField] private int telemetryMaxPayloadBytes = 10_000;

        [Header("Determinism")]
        [Tooltip("Fixed delta time applied on boot for deterministic sim.")]
        [SerializeField] private float fixedDeltaTime = 1f / 60f;
        [Tooltip("Seed used for UnityEngine.Random when not provided by server.")]
        [SerializeField] private int defaultRandomSeed = 12345;

        public string BaseUrl => baseUrl?.TrimEnd('/');
        public string ConfigSigningKey => configSigningKey;
        public float TelemetryFlushSeconds => telemetryFlushSeconds;
        public int TelemetryMaxBatchSize => telemetryMaxBatchSize;
        public int TelemetryMaxPayloadBytes => telemetryMaxPayloadBytes;
        public float FixedDeltaTime => fixedDeltaTime;
        public int DefaultRandomSeed => defaultRandomSeed;
        public IReadOnlyList<FeatureToggleSetting> FeatureToggles => featureToggles;
    }

    [Serializable]
    public sealed class FeatureToggleSetting
    {
        public string key;
        public bool enabled = true;
    }
}

