using System.Collections;
using System.Collections.Generic;
using Biopulse.Calibration;
using Biopulse.Emg;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class CalibrationWizardTests
    {
        private string testSource;
        private bool loadedScene;
        private Scene scene;
        private bool background;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (testSource != null) ProfileStore.Delete(testSource);
            Application.runInBackground = background;
            if (loadedScene && scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest]
        public IEnumerator ReplayCalibratesSavesAndCompletesAllTutorialLessons()
        {
            background = Application.runInBackground; Application.runInBackground = true;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/CalibrationTutorial.unity");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync("CalibrationTutorial",LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath("Assets/Scenes/CalibrationTutorial.unity"); loadedScene = true;
            }
            CalibrationTutorial wizard = null;
            foreach(var root in scene.GetRootGameObjects())
            { var candidate = root.GetComponentInChildren<CalibrationTutorial>(true); if(candidate != null) wizard = candidate; }
            Assert.That(wizard,Is.Not.Null);
            var input = wizard.Input;
            input.SendMessage("OnApplicationFocus",true); input.SendMessage("OnApplicationPause",false);
            testSource = "test-wizard-" + System.Guid.NewGuid().ToString("N");
            var frames = new List<EmgSample>();
            for(int i=0;i<=4000;i++)
            {
                double t = i*.005; float flex = 0, extend = 0;
                for(int rep=0;rep<3;rep++)
                {
                    if(t >= 3.3+rep*.65 && t < 3.65+rep*.65) flex = .6f;
                    if(t >= 5.3+rep*.65 && t < 5.65+rep*.65) extend = .6f;
                }
                if(t>=7.7 && t<8.0) flex=.6f;
                if(t>=8.3 && t<8.6) extend=.6f;
                if(t>=8.9 && t<9.3) flex=extend=.6f;
                if(t>=9.8 && t<10.1) extend=.6f;
                if(t>=10.9 && t<11.2) extend=.25f;
                if(t>=12.2 && t<12.5) extend=.6f;
                if(t>=13.6 && t<13.9) flex=.3f;
                if(t>=15.1 && t<15.4) extend=.6f;
                if(t>=16.0 && t<16.3) flex=.6f;
                if(t>=16.7 && t<17.0) extend=.6f;
                if(t>=17.5 && t<18.3) flex=extend=.6f;
                frames.Add(new EmgSample(i,t,flex,extend));
            }
            var replay = new ReplayEmgSource(frames,()=>Time.realtimeSinceStartupAsDouble,testSource);
            input.SetSource(replay,new EmgCalibration(testSource));
            input.AllowBackgroundReplayInEditor = true;
            yield return null; yield return null;
            wizard.StartCalibration();
            string previous = ""; var trace = new List<string>();
            input.SampleReceived += sample =>
            {
                string current = wizard.Stage + "/" + wizard.Recording?.Stage + "/" + wizard.Recording?.Repetitions;
                if (current != previous) { trace.Add($"{sample.TimeSeconds:F3}: {current}"); previous = current; }
                if (wizard.Stage == SetupStage.VerifyRest && sample.Sequence % 20 == 0)
                    trace.Add($"verify {sample.TimeSeconds:F3}: {input.Pipeline.State}, duration {wizard.RestDuration:F2}, valid {input.Pipeline.Frame.IsValid}, waiting {input.Pipeline.WaitingForRest}, resume {input.Pipeline.ResumeRequired}");
            };
            yield return new WaitForSecondsRealtime(9.45f);
            Assert.That(wizard.Stage,Is.EqualTo(SetupStage.Review),
                $"Input: {input.Pipeline.State}, fault: {input.Pipeline.Fault}, rest: {input.Pipeline.WaitingForRest}, resume: {input.Pipeline.ResumeRequired}, wizardEnabled: {wizard.isActiveAndEnabled}, restDuration: {wizard.RestDuration}, frame: {input.Pipeline.Frame.TimeSeconds}. {string.Join("; ",trace)}");
            Assert.That(ProfileStore.TryLoad(replay.Info,input.SettingsSignature,out _),Is.False,
                "Successful measurement must not save without explicit confirmation.");
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(ProfileStore.TryLoad(replay.Info,input.SettingsSignature,out var saved),Is.True);
            Assert.That(saved.verified,Is.True); Assert.That(saved.flexReference,Is.EqualTo(.6f).Within(.001));
            Assert.That(saved.extendReference,Is.EqualTo(.6f).Within(.001));
            Assert.That(wizard.Stage,Is.EqualTo(SetupStage.Tutorial)); Assert.That(wizard.Tutorial,Is.Not.Null);
            yield return new WaitForSecondsRealtime(8.3f);
            Assert.That(wizard.Stage,Is.EqualTo(SetupStage.Complete),
                $"Tutorial stopped at {wizard.Tutorial.Step}. Input fault: {input.Pipeline.Fault}");
        }
    }
}
