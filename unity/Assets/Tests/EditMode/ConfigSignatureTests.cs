using System.IO;
using System.Reflection;
using Armada.Client.Core;
using Armada.Client.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace Armada.Client.Tests.EditMode
{
    public sealed class ConfigSignatureTests
    {
        [Test]
        public void ConfigSignature_AcceptsServerFixtureAndRejectsTampering()
        {
            // tests/config-signature.test.ts pins this public test fixture to
            // the real server route response; no runtime secret is required.
            var fixture = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
                "Tests", "EditMode", "config-signature.json")));
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var response = fixture["response"].ToObject<ConfigResponse>();
            var payload = JsonConvert.SerializeObject(response.Config.Content, settings);
            var key = (string)fixture["testOnlySigningKey"];
            var verify = typeof(ConfigService).GetMethod("VerifySignature",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(verify, Is.Not.Null);
            bool Valid(string body, string signature, string signingKey) =>
                (bool)verify.Invoke(null, new object[] { body, signature, signingKey });

            Assert.That(Valid(payload, response.Signature, key), Is.True, "server hex HMAC must verify");
            Assert.That(Valid(payload.Replace("Starter sails", "Tampered sails"), response.Signature, key), Is.False);
            var changed = (response.Signature[0] == '0' ? "1" : "0") + response.Signature.Substring(1);
            Assert.That(Valid(payload, changed, key), Is.False);
            Assert.That(Valid(payload, response.Signature, "wrong-test-key"), Is.False);
            Assert.That(Valid(payload, "", key), Is.False);
            Assert.That(Valid(payload, response.Signature, ""), Is.False);
            Assert.That(Valid(payload, "not-hex", key), Is.False);
        }
    }
}
