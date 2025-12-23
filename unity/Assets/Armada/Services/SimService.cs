using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class SimService
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private readonly JsonSerializerOptions _json;
        private const string FeatureKey = "sim";

        public SimService(ApiClient client, FeatureFlags flags, JsonSerializerOptions json)
        {
            _client = client;
            _flags = flags;
            _json = json;
        }

        public async Task<SimPreviewResult> PreviewAsync(SimPreviewRequest request)
        {
            var requestHash = ComputeHash(request);
            Debug.Log($"[Sim] Request hash {requestHash}");

            var resp = await _client.SendAsync<SimPreviewEnvelope>("/sim/preview", UnityWebRequest.kHttpVerbPOST, request);
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                _flags.DisableFromForbidden(FeatureKey);
                return null;
            }

            if (!resp.Success)
            {
                Debug.LogError($"[Sim] Preview failed: {resp.StatusCode} {resp.RawBody}");
                return null;
            }

            var result = resp.Data?.Result;
            if (result != null)
            {
                var responseHash = ComputeHash(result);
                Debug.Log($"[Sim] Response hash {responseHash}, server hash {result.Hash}");
            }

            return result;
        }

        private string ComputeHash<T>(T payload)
        {
            var json = JsonSerializer.Serialize(payload, _json);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}

