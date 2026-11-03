using System.Collections;
using System.Collections.Generic;
using Biopulse.Emg;
using Biopulse.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class EmgInputDriverTests
    {
        private GameObject gameObject;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (gameObject != null) Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplayCanReplaceEmulatorAndDeliverAnIntentionThroughDriver()
        {
            gameObject = new GameObject("Input driver integration test");
            var driver = gameObject.AddComponent<EmgInputDriver>();
            var previousSource = driver.Source;
            int intentions = 0;
            EmgIntent last = default;
            driver.IntentRecognized += intent => { intentions++; last = intent; };
            var samples = new List<EmgSample>();
            for (int i = 0; i <= 60; i++) samples.Add(new EmgSample(i, i * .005, 0, i >= 10 ? .8f : 0));
            var replay = new ReplayEmgSource(samples, () => Time.realtimeSinceStartupAsDouble);
            driver.SetSource(replay, new EmgCalibration(replay.Info.Id));
            Assert.That(previousSource.Status, Is.EqualTo(EmgSourceStatus.Stopped));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(intentions, Is.EqualTo(1));
            Assert.That(last.Kind, Is.EqualTo(EmgIntentKind.Extend));
            Assert.That(last.Extensor, Is.GreaterThan(.25f));
            Assert.That(driver.Pipeline.Frame.IsValid, Is.True);
        }

        [UnityTest]
        public IEnumerator StartupNotificationsDoNotPauseButRealFocusLossRequiresResume()
        {
            gameObject = new GameObject("Input lifecycle notification test");
            var driver = gameObject.AddComponent<EmgInputDriver>();
            yield return null;
            gameObject.SendMessage("OnApplicationPause", false);
            gameObject.SendMessage("OnApplicationFocus", true);
            yield return null;
            yield return null;
            Assert.That(driver.Pipeline.ResumeRequired, Is.False);
            Assert.That(driver.Pipeline.Frame.IsValid, Is.True);
            gameObject.SendMessage("OnApplicationFocus", false);
            Assert.That(driver.Pipeline.ResumeRequired, Is.True);
            Assert.That(driver.Pipeline.Frame.IsValid, Is.False);
            gameObject.SendMessage("OnApplicationFocus", true);
            yield return null;
            yield return null;
            Assert.That(driver.Pipeline.ResumeRequired, Is.True);
            Assert.That(driver.Pipeline.Resume(), Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingDriverStopsSourceAndReenableStartsAtRest()
        {
            gameObject = new GameObject("Input driver lifecycle test");
            var driver = gameObject.AddComponent<EmgInputDriver>();
            yield return null;
            var source = driver.Source;
            driver.enabled = false;
            Assert.That(source.Status, Is.EqualTo(EmgSourceStatus.Stopped));
            Assert.That(driver.Source, Is.Null);
            driver.enabled = true;
            yield return null;
            yield return null;
            Assert.That(driver.Source, Is.Not.SameAs(source));
            Assert.That(driver.Pipeline.State, Is.EqualTo(EmgIntentKind.Rest));
            Assert.That(driver.Pipeline.Frame.IsValid, Is.True);
        }
    }
}
