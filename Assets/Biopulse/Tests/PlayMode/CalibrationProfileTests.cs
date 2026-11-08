using System.Collections;
using Biopulse.Calibration;
using Biopulse.Emg;
using Biopulse.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class CalibrationProfileTests
    {
        [Test]
        public void OnlyVerifiedMatchingProfileCanBeSavedAndLoaded()
        {
            string id = "test-profile-" + System.Guid.NewGuid().ToString("N");
            var info = new EmgSourceInfo(id,EmgRepresentation.Envelope,"arbitrary");
            var profile = CalibrationProfile.Create(info,"settings",new EmgCalibration(id,0,.6f,0,.7f));
            try
            {
                Assert.Throws<System.InvalidOperationException>(()=>ProfileStore.Save(profile));
                profile.verified = true; ProfileStore.Save(profile);
                Assert.That(ProfileStore.TryLoad(info,"settings",out var restored),Is.True);
                Assert.That(restored.ToCalibration().FlexReference,Is.EqualTo(.6f));
                Assert.That(ProfileStore.TryLoad(info,"changed settings",out _),Is.False);
                Assert.That(ProfileStore.TryLoad(new EmgSourceInfo(id,EmgRepresentation.Raw,"arbitrary"),"settings",out _),Is.False);
                Assert.That(ProfileStore.TryLoad(new EmgSourceInfo(id,EmgRepresentation.Envelope,"volts"),"settings",out _),Is.False);
            }
            finally { ProfileStore.Delete(id); }
        }
        [UnityTest]
        public IEnumerator BackgroundReplayOptionDoesNotBypassEmulatorFocusLoss()
        {
            var go = new GameObject("Replay-only focus policy test");
            try
            {
                var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
                driver.AllowBackgroundReplayInEditor = true;
                yield return null;
                go.SendMessage("OnApplicationFocus",false);
                Assert.That(driver.Pipeline.ResumeRequired,Is.True);
                Assert.That(driver.Pipeline.Frame.IsValid,Is.False);
            }
            finally { Object.Destroy(go); }
        }
        [UnityTest]
        public IEnumerator ApplyingProfileFromSampleCallbackDoesNotCreateFalseInputLoss()
        {
            var go = new GameObject("Profile callback lifecycle test");
            try
            {
                var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
                bool applied = false;
                driver.SampleReceived += _ =>
                {
                    if (applied) return; applied = true;
                    driver.ApplyCalibration(new EmgCalibration(driver.Source.Info.Id,0,.6f,0,.6f));
                };
                yield return null; yield return null; yield return null;
                Assert.That(applied,Is.True); Assert.That(driver.Pipeline.ResumeRequired,Is.False);
                Assert.That(driver.Pipeline.Frame.IsValid,Is.True);
            }
            finally { Object.Destroy(go); }
        }
        [UnityTest]
        public IEnumerator ApplyingProfileKeepsSourceAndRequiresRealRest()
        {
            var go = new GameObject("Profile application test");
            try
            {
                var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
                var samples = new System.Collections.Generic.List<EmgSample>();
                for(int i=0;i<=200;i++) samples.Add(new EmgSample(i,i*.005,0,(i>=20 && i<85)||i>=120? .7f:0));
                var source = new ReplayEmgSource(samples,()=>Time.realtimeSinceStartupAsDouble);
                driver.SetSource(source,new EmgCalibration(source.Info.Id));
                int count = 0; driver.IntentRecognized += _=>count++;
                yield return new WaitForSecondsRealtime(.22f);
                int previous = count;
                driver.ApplyCalibration(new EmgCalibration(source.Info.Id,0,.6f,0,.7f,2));
                Assert.That(driver.Source,Is.SameAs(source)); Assert.That(source.Status,Is.EqualTo(EmgSourceStatus.Streaming));
                yield return new WaitForSecondsRealtime(.14f); Assert.That(count,Is.EqualTo(previous));
                yield return new WaitForSecondsRealtime(.4f); Assert.That(count,Is.EqualTo(previous+1));
                Assert.That(driver.Pipeline.Frame.CalibrationVersion,Is.EqualTo(2));
            }
            finally { Object.Destroy(go); }
        }
    }
}
