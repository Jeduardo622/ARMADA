using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Armada.Client.Core
{
    public sealed class AuthState
    {
        public string Token;
        public Player Player;
    }

    public sealed class AuthService : IAuthProvider
    {
        private readonly ApiClient _apiClient;
        private readonly JsonSerializerSettings _options;
        private AuthState _state;
        private Task<string> _inFlightRequest;

        public AuthService(ApiClient apiClient, JsonSerializerSettings options)
        {
            _apiClient = apiClient;
            _options = options;
        }

        public bool HasToken => _state != null && !string.IsNullOrWhiteSpace(_state.Token);

        // Single-threaded Unity main-thread access is assumed, matching the
        // rest of the client service graph; no locking.
        public Task<string> GetTokenAsync()
        {
            if (HasToken)
            {
                return Task.FromResult(_state.Token);
            }

            // Concurrent callers share the one in-flight request. The old
            // bool guard made late callers yield once and return null, so a
            // startup race (e.g. a UI refresh beside a bootstrap) sent their
            // follow-up requests unauthenticated.
            if (_inFlightRequest != null)
            {
                return _inFlightRequest;
            }

            var request = RequestTokenAsync();
            if (!request.IsCompleted)
            {
                _inFlightRequest = request;
            }

            return request;
        }

        private async Task<string> RequestTokenAsync()
        {
            try
            {
                var response = await _apiClient.SendAsync<GuestAuthResponse>("/auth/guest", UnityWebRequest.kHttpVerbPOST, new GuestAuthRequest(), null, false);
                if (response.Success && response.Data != null)
                {
                    _state = new AuthState
                    {
                        Token = response.Data.Token,
                        Player = response.Data.Player
                    };
                    Debug.Log($"[Auth] Token acquired for player {_state.Player?.Id}");
                    return _state.Token;
                }

                Debug.LogError($"[Auth] Failed to obtain token: {response.StatusCode} {response.RawBody}");
                return null;
            }
            finally
            {
                // Cleared on completion (success or failure) so a failed
                // request is retried by the next caller instead of pinning a
                // stale completed task.
                _inFlightRequest = null;
            }
        }

        public Player CurrentPlayer => _state?.Player;
    }
}

