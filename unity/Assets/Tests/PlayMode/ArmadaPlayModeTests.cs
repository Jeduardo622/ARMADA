using System.Collections;
using Armada.Client.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Armada.Client.Tests.PlayMode
{
    public sealed class ArmadaPlayModeTests
    {
        [UnityTest]
        public IEnumerator DeterministicSeed_RepeatsAcrossFrames()
        {
            var originalState = UnityEngine.Random.state;
            var gameObject = new GameObject("playmode-determinism-test");
            try
            {
                var hooks = gameObject.AddComponent<DeterministicSimHooks>();

                hooks.ApplySeed(9001);
                var expected = UnityEngine.Random.Range(0, int.MaxValue);

                yield return null;

                hooks.ApplySeed(9001);
                Assert.That(UnityEngine.Random.Range(0, int.MaxValue), Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Random.state = originalState;
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
