using System;
using System.Text.Json;
using System.Threading.Tasks;
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
        private readonly JsonSerializerOptions _options;
        private AuthState _state;
        private bool _isRequestInFlight;

        public AuthService(ApiClient apiClient, JsonSerializerOptions options)
        {
            _apiClient = apiClient;
            _options = options;
        }

        public bool HasToken => _state != null && !string.IsNullOrWhiteSpace(_state.Token);

        public async Task<string> GetTokenAsync()
        {
            if (HasToken)
            {
                return _state.Token;
            }

            if (_isRequestInFlight)
            {
                await Task.Yield();
                return _state?.Token;
            }

            _isRequestInFlight = true;
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
                _isRequestInFlight = false;
            }
        }

        public Player CurrentPlayer => _state?.Player;
    }
}

