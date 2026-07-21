using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class SimService : ISimPreviewClient
    {
        private readonly ApiClient _client;
        private readonly FeatureFlags _flags;
        private readonly JsonSerializerSettings _json;
        private const string FeatureKey = "sim";

        public SimService(ApiClient client, FeatureFlags flags, JsonSerializerSettings json)
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
            var json = JsonConvert.SerializeObject(payload, _json);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}

