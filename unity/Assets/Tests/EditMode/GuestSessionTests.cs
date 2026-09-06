using System;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Armada.Client.Core;
using NUnit.Framework;

namespace Armada.Client.Tests
{
    public sealed class GuestSessionTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        private const string Secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private sealed class Store : IGuestCredentialStore
        {
            public bool IsSupported { get; set; } = true;
            public string Value;
            public bool FailSave;
            public bool FailLoad;
            public string Load() { if (FailLoad) throw new InvalidOperationException(); return Value; }
            public void Save(string credential) { if (FailSave) throw new InvalidOperationException(); Value = credential; }
        }

        private sealed class Transport : IGuestAuthTransport
        {
            public int Registrations;
            public int Refreshes;
            public string LastCredential;
            public Task<ApiResponse<GuestAuthResponse>> Response = Task.FromResult(Success());
            public Task<ApiResponse<GuestAuthResponse>> RegisterAsync() { Registrations++; return Response; }
            public Task<ApiResponse<GuestAuthResponse>> RefreshAsync(string credential) { Refreshes++; LastCredential = credential; return Response; }
        }

        private static ApiResponse<GuestAuthResponse> Success(DateTimeOffset? expiry = null, bool credential = true) =>
            ApiResponse<GuestAuthResponse>.CreateSuccess(new GuestAuthResponse {
                Token = "access-token", Player = new Player { Id = "same-player" },
                GuestCredential = credential ? Secret : null,
                AccessExpiresAt = expiry ?? Now.AddHours(1), CredentialExpiresAt = Now.AddDays(180)
            }, HttpStatusCode.OK, null);

        [Test]
        public void RegistrationPersistsBeforeSuccessAndRestartRefreshesSameIdentity()
        {
            var store = new Store();
            var transport = new Transport();
            var auth = new AuthService(transport, store, () => Now);
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("access-token"));
            Assert.That(store.Value, Is.EqualTo(Secret));
            Assert.That(transport.Registrations, Is.EqualTo(1));
            var restarted = new AuthService(transport, store, () => Now);
            Assert.That(restarted.GetTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("access-token"));
            Assert.That(restarted.CurrentPlayer.Id, Is.EqualTo("same-player"));
            Assert.That(transport.Refreshes, Is.EqualTo(1));
            Assert.That(transport.LastCredential, Is.EqualTo(Secret));
            Assert.That(transport.Registrations, Is.EqualTo(1));
        }

        [Test]
        public void SaveFailureWithholdsTokenAndRetriesSavingWithoutCreatingAnotherGuest()
        {
            var store = new Store { FailSave = true };
            var transport = new Transport();
            var auth = new AuthService(transport, store, () => Now);
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(auth.HasToken, Is.False);
            Assert.That(auth.LastError, Is.EqualTo("storage_unavailable"));
            store.FailSave = false;
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("access-token"));
            Assert.That(transport.Registrations, Is.EqualTo(1));
            Assert.That(store.Value, Is.EqualTo(Secret));
        }

        [Test]
        public void ExpiredAccessTokenRefreshesWithSingleFlight()
        {
            var clock = Now;
            var store = new Store();
            var transport = new Transport();
            var auth = new AuthService(transport, store, () => clock);
            auth.GetTokenAsync().GetAwaiter().GetResult();
            clock = Now.AddHours(2);
            var pending = new TaskCompletionSource<ApiResponse<GuestAuthResponse>>();
            transport.Response = pending.Task;
            var first = auth.GetTokenAsync();
            var second = auth.GetTokenAsync();
            Assert.That(first, Is.SameAs(second));
            Assert.That(auth.HasToken, Is.False);
            pending.SetResult(Success(clock.AddHours(1), false));
            Assert.That(first.GetAwaiter().GetResult(), Is.EqualTo("access-token"));
            Assert.That(transport.Refreshes, Is.EqualTo(1));
        }

        [TestCase(HttpStatusCode.ServiceUnavailable, "offline")]
        [TestCase(HttpStatusCode.Unauthorized, "session_invalid")]
        public void RefreshFailureKeepsIdentityAndNeverRegistersReplacement(HttpStatusCode status, string error)
        {
            var store = new Store { Value = Secret };
            var transport = new Transport { Response = Task.FromResult(ApiResponse<GuestAuthResponse>.CreateFailure(status, "sensitive-response", null)) };
            var auth = new AuthService(transport, store, () => Now);
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(auth.LastError, Is.EqualTo(error));
            Assert.That(store.Value, Is.EqualTo(Secret));
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(transport.Registrations, Is.Zero);
            transport.Response = Task.FromResult(Success(credential: false));
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("access-token"));
        }

        [Test]
        public void LegacyTokenOnlyRegistrationIsNotAcceptedAsDurableSession()
        {
            var transport = new Transport { Response = Task.FromResult(Success(credential: false)) };
            var auth = new AuthService(transport, new Store(), () => Now);
            Assert.That(auth.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(auth.LastError, Is.EqualTo("invalid_auth_response"));
        }

        [Test]
        public void StorageUnavailableOrUnsupportedNeverRegisters()
        {
            var transport = new Transport();
            var unsupported = new AuthService(transport, new Store { IsSupported = false }, () => Now);
            Assert.That(unsupported.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(unsupported.LastError, Is.EqualTo("storage_unsupported"));
            var corrupt = new AuthService(transport, new Store { FailLoad = true }, () => Now);
            Assert.That(corrupt.GetTokenAsync().GetAwaiter().GetResult(), Is.Null);
            Assert.That(corrupt.LastError, Is.EqualTo("storage_unavailable"));
            Assert.That(transport.Registrations, Is.Zero);
        }

        [Test]
        public void CredentialNamespaceUsesOriginAndRejectsCleartextRemoteTransport()
        {
            Assert.That(GuestCredentialStore.OriginNamespace("https://EXAMPLE.com:443/api"), Is.EqualTo(GuestCredentialStore.OriginNamespace("https://example.com/")));
            Assert.That(GuestCredentialStore.OriginNamespace("https://example.com"), Is.Not.EqualTo(GuestCredentialStore.OriginNamespace("https://stage.example.com")));
            Assert.That(GuestCredentialStore.OriginNamespace("http://localhost:4500"), Is.Not.EqualTo(GuestCredentialStore.OriginNamespace("http://localhost:4501")));
            Assert.Throws<ArgumentException>(() => GuestCredentialStore.OriginNamespace("http://example.com"));
        }

        [Test]
        public void RegistrationAndRefreshJsonMatchBackendContract()
        {
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(new GuestAuthRequest()), Is.EqualTo("{}"));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(new GuestRefreshRequest { GuestCredential = Secret }),
                Is.EqualTo("{\"guestCredential\":\"" + Secret + "\"}"));
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [Test]
        public void WindowsProtectedStoreSurvivesReopenAndRejectsTamperingAndOtherOrigin()
        {
            var directory = Path.Combine(Path.GetTempPath(), "armada-guest-store-test-" + Guid.NewGuid().ToString("N"));
            var origin = GuestCredentialStore.OriginNamespace("https://example.com");
            var protection = new GuestCredentialStore.WindowsProtection(origin);
            try
            {
                var store = new GuestCredentialStore.ProtectedFileStore(directory, origin, protection);
                Assert.That(store.Load(), Is.Null);
                store.Save(Secret);
                var path = Directory.GetFiles(directory)[0];
                var ciphertext = File.ReadAllBytes(path);
                Assert.That(Encoding.UTF8.GetString(ciphertext), Does.Not.Contain(Secret));
                var reopened = new GuestCredentialStore.ProtectedFileStore(directory, origin, protection);
                Assert.That(reopened.Load(), Is.EqualTo(Secret));
                reopened.Save(Secret);
                Assert.That(reopened.Load(), Is.EqualTo(Secret));
                Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
                var other = new GuestCredentialStore.WindowsProtection(GuestCredentialStore.OriginNamespace("https://other.example.com"));
                Assert.Throws<CryptographicException>(() => other.Unprotect(ciphertext));
                ciphertext[ciphertext.Length - 1] ^= 1;
                File.WriteAllBytes(path, ciphertext);
                Assert.Throws<CryptographicException>(() => reopened.Load());
            }
            finally
            {
                // Remove only this test's known ciphertext file, never recurse
                // through an unexpectedly populated temporary directory.
                if (Directory.Exists(directory))
                {
                    File.Delete(Path.Combine(directory, "guest-" + origin + ".credential"));
                    Directory.Delete(directory);
                }
            }
        }
#endif
    }
}
