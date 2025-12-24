using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Armada.Client.Core
{
    public interface IAuthProvider
    {
        Task<string> GetTokenAsync();
    }

    public sealed class ApiClient
    {
        private readonly string _baseUrl;
        private readonly IAuthProvider _authProvider;
        private JsonSerializerSettings _jsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public ApiClient(string baseUrl, IAuthProvider authProvider, JsonSerializerSettings jsonSettings = null)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            _authProvider = authProvider;
            if (jsonSettings != null)
            {
                _jsonSettings = jsonSettings;
            }
        }

        public async Task<ApiResponse<T>> SendAsync<T>(string path, string method, object body = null, Dictionary<string, string> headers = null, bool requiresAuth = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path required", nameof(path));

            var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : $"{_baseUrl}{path}";
            var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body, _jsonSettings);
                var payload = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    request.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }

            if (requiresAuth && _authProvider != null && !IsHealthPath(path))
            {
                var token = await _authProvider.GetTokenAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                }
            }

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

            var status = request.responseCode > 0 ? (HttpStatusCode)request.responseCode : HttpStatusCode.ServiceUnavailable;
            var raw = request.downloadHandler?.text ?? string.Empty;
            string etag = null;
            if (request.GetResponseHeaders() != null && request.GetResponseHeaders().TryGetValue("ETag", out var etagHeader))
            {
                etag = etagHeader;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return ApiResponse<T>.CreateFailure(status, raw, etag, "offline");
            }

            if (typeof(T) == typeof(string))
            {
                return ApiResponse<T>.CreateSuccess((T)(object)raw, status, etag);
            }

            T data = default;
            try
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    data = JsonConvert.DeserializeObject<T>(raw, _jsonSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] Failed to deserialize {typeof(T).Name} from {url}: {ex.Message}");
            }

            if (status is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
            {
                return ApiResponse<T>.CreateSuccess(data, status, etag, raw);
            }

            return ApiResponse<T>.CreateFailure(status, raw, etag);
        }

        private static bool IsHealthPath(string path)
        {
            return path.Contains("/healthz", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/readyz", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ApiResponse<T>
    {
        public bool Success { get; }
        public HttpStatusCode StatusCode { get; }
        public T Data { get; }
        public string RawBody { get; }
        public string ETag { get; }
        public string ErrorReason { get; }

        private ApiResponse(bool success, HttpStatusCode statusCode, T data, string etag, string rawBody, string errorReason)
        {
            Success = success;
            StatusCode = statusCode;
            Data = data;
            ETag = etag;
            RawBody = rawBody;
            ErrorReason = errorReason;
        }

        public static ApiResponse<T> CreateSuccess(T data, HttpStatusCode statusCode, string etag, string rawBody = null)
        {
            return new ApiResponse<T>(true, statusCode, data, etag, rawBody, null);
        }

        public static ApiResponse<T> CreateFailure(HttpStatusCode statusCode, string rawBody, string etag, string reason = null)
        {
            return new ApiResponse<T>(false, statusCode, default, etag, rawBody, reason);
        }
    }
}

