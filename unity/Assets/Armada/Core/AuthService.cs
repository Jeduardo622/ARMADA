using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Armada.Client.Core
{
    public sealed class AuthState
    {
        public string Token;
        public Player Player;
        public DateTimeOffset AccessExpiresAt;
    }

    public interface IGuestAuthTransport
    {
        Task<ApiResponse<GuestAuthResponse>> RegisterAsync();
        Task<ApiResponse<GuestAuthResponse>> RefreshAsync(string credential);
    }

    public sealed class GuestAuthTransport : IGuestAuthTransport
    {
        private readonly ApiClient _client;
        public GuestAuthTransport(ApiClient client) { _client = client; }
        public Task<ApiResponse<GuestAuthResponse>> RegisterAsync() =>
            _client.SendAsync<GuestAuthResponse>("/auth/guest", UnityWebRequest.kHttpVerbPOST, new GuestAuthRequest(), null, false);
        public Task<ApiResponse<GuestAuthResponse>> RefreshAsync(string credential) =>
            _client.SendAsync<GuestAuthResponse>("/auth/refresh", UnityWebRequest.kHttpVerbPOST,
                new GuestRefreshRequest { GuestCredential = credential }, null, false);
    }

    public sealed class AuthService : IAuthProvider
    {
        private readonly IGuestAuthTransport _transport;
        private readonly IGuestCredentialStore _store;
        private readonly Func<DateTimeOffset> _now;
        private AuthState _state;
        private Task<string> _inFlightRequest;
        private bool _loaded;
        private string _credential;
        private GuestAuthResponse _pendingRegistration;

        // Existing bootstraps keep their constructor; all real sessions now
        // require protected persistence before a token can escape this service.
        public AuthService(ApiClient apiClient, JsonSerializerSettings options)
            : this(new GuestAuthTransport(apiClient), GuestCredentialStore.Create(apiClient?.BaseUrl), () => DateTimeOffset.UtcNow) { }

        public AuthService(IGuestAuthTransport transport, IGuestCredentialStore store, Func<DateTimeOffset> now)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public bool HasToken => _state != null && !string.IsNullOrWhiteSpace(_state.Token)
            && _state.AccessExpiresAt > _now().AddSeconds(30);
        public Player CurrentPlayer => _state?.Player;
        public string LastError { get; private set; }

        // Unity main-thread callers share startup and refresh. No protected
        // gameplay request is automatically replayed by this service.
        public Task<string> GetTokenAsync()
        {
            if (HasToken) return Task.FromResult(_state.Token);
            if (_inFlightRequest != null) return _inFlightRequest;
            var request = RequestTokenAsync();
            if (!request.IsCompleted) _inFlightRequest = request;
            return request;
        }

        private async Task<string> RequestTokenAsync()
        {
            try
            {
                LastError = null;
                if (!_store.IsSupported) return Fail("storage_unsupported");
                try
                {
                    if (!_loaded)
                    {
                        _credential = _store.Load();
                        if (_credential != null && !ValidCredential(_credential)) return Fail("storage_unavailable");
                        _loaded = true;
                    }
                    if (_pendingRegistration != null)
                    {
                        PersistRegistration();
                        if (HasToken) return _state.Token;
                    }
                }
                catch { return Fail("storage_unavailable"); }

                var restoring = !string.IsNullOrEmpty(_credential);
                ApiResponse<GuestAuthResponse> response;
                try
                {
                    response = restoring ? await _transport.RefreshAsync(_credential) : await _transport.RegisterAsync();
                }
                catch { return Fail("offline"); }
                if (response == null || !response.Success)
                    return Fail(response?.StatusCode == HttpStatusCode.Unauthorized ? "session_invalid" : "offline");

                var session = response.Data;
                if (session == null || string.IsNullOrWhiteSpace(session.Token) || string.IsNullOrWhiteSpace(session.Player?.Id)
                    || session.AccessExpiresAt <= _now().AddSeconds(30) || session.CredentialExpiresAt <= _now()
                    || (!restoring && !ValidCredential(session.GuestCredential)))
                    return Fail("invalid_auth_response");

                if (!restoring)
                {
                    // Retain the issued response while a save is retried; a
                    // storage outage must not register another guest on retry.
                    _pendingRegistration = session;
                    try { PersistRegistration(); }
                    catch { return Fail("storage_unavailable"); }
                }
                else SetState(session);
                return _state.Token;
            }
            finally { _inFlightRequest = null; }
        }

        private void PersistRegistration()
        {
            _store.Save(_pendingRegistration.GuestCredential);
            _credential = _pendingRegistration.GuestCredential;
            SetState(_pendingRegistration);
            _pendingRegistration = null;
        }

        private void SetState(GuestAuthResponse session) => _state = new AuthState {
            Token = session.Token, Player = session.Player, AccessExpiresAt = session.AccessExpiresAt
        };
        private static bool ValidCredential(string value) => value != null && Regex.IsMatch(value, "^[A-Za-z0-9_-]{43}$");
        private string Fail(string reason) { LastError = reason; return null; }
    }
}
