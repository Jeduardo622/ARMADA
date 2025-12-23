using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Armada.Client.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Services
{
    public sealed class ConfigService
    {
        private readonly ApiClient _client;
        private readonly string _signingKey;
        private readonly JsonSerializerOptions _json;
        private readonly Dictionary<string, (ConfigSnapshot snapshot, string etag)> _cache = new();

        public ConfigService(ApiClient client, string signingKey, JsonSerializerOptions json)
        {
            _client = client;
            _signingKey = signingKey;
            _json = json;
        }

        public async Task<ConfigSnapshot> FetchAsync(string ns)
        {
            var headers = new Dictionary<string, string>();
            if (_cache.TryGetValue(ns, out var cached) && !string.IsNullOrWhiteSpace(cached.etag))
            {
                headers["If-None-Match"] = cached.etag;
            }

            var response = await _client.SendAsync<ConfigResponse>($"/config/{ns}", UnityWebRequest.kHttpVerbGET, null, headers);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cached.snapshot != null)
            {
                return cached.snapshot;
            }

            if (!response.Success || response.Data?.Config == null)
            {
                Debug.LogError($"[Config] Failed to fetch namespace {ns}: {response.StatusCode} {response.RawBody}");
                return null;
            }

            var payloadJson = JsonSerializer.Serialize(response.Data.Config.Content, _json);
            if (!VerifySignature(payloadJson, response.Data.Signature, _signingKey))
            {
                Debug.LogError($"[Config] Signature verification failed for {ns}");
                return null;
            }

            _cache[ns] = (response.Data.Config, response.ETag);
            Debug.Log($"[Config] Loaded namespace {ns} v{response.Data.Config.Version} (etag {response.ETag})");
            return response.Data.Config;
        }

        private static bool VerifySignature(string payload, string signatureBase64, string secret)
        {
            if (string.IsNullOrWhiteSpace(signatureBase64) || string.IsNullOrWhiteSpace(secret))
            {
                return false;
            }

            try
            {
                var key = Encoding.UTF8.GetBytes(secret);
                using var hmac = new HMACSHA256(key);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty));
                var computed = Convert.ToBase64String(hash);
                return string.Equals(computed, signatureBase64, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Config] Signature verification error: {ex.Message}");
                return false;
            }
        }
    }
}

