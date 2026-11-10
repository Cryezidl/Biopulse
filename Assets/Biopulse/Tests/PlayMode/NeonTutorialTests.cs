using System.Collections;
using System.Collections.Generic;
using Biopulse.Neon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class NeonTutorialTests
    {
        private bool background;
        private int coins;
        private bool hadCoins;

        [SetUp]
        public void Setup()
        {
            background = Application.runInBackground;
            hadCoins = PlayerPrefs.HasKey(NeonProgress.CoinsKey);
            coins = NeonProgress.TotalCoins;
            Application.runInBackground = true;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            Application.runInBackground = background;
            if (hadCoins) PlayerPrefs.SetInt(NeonProgress.CoinsKey, coins);
            else PlayerPrefs.DeleteKey(NeonProgress.CoinsKey);
            PlayerPrefs.Save();
            var course = SceneManager.GetSceneByName("Neon_Tutorial");
            if (course.IsValid() && course.isLoaded)
            {
                var empty = SceneManager.CreateScene("Tutorial test cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(course);
            }
            else yield return null;
        }

        [UnityTest]
        public IEnumerator CourseFreezesRejectsWrongInputAndCompletesEveryMechanic()
        {
            yield return SceneManager.LoadSceneAsync("Neon_Tutorial", LoadSceneMode.Single);
            yield return null;
            var session = NeonLevelSession.Current;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.IsTutorial, Is.True);
            Assert.That(session.FinishX, Is.EqualTo(331.2f / 2).Within(.01f));
            Assert.That(Object.FindObjectsByType<NeonChaser>(FindObjectsSortMode.None), Is.Empty);
            var input = session.Input;
            input.AllowUnfocusedScriptedInput = true;
            input.ScriptedFlex = input.ScriptedExtend = 0;
            input.SendMessage("OnApplicationFocus", true);
            input.SendMessage("OnApplicationPause", false);
            var tutorial = session.Tutorial;
            float deadline = Time.realtimeSinceStartup + 12;
            while (!tutorial.IsPaused && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(tutorial.IsPaused, Is.True, "The first obstacle must stop the world.");
            Assert.That(tutorial.CurrentKind, Is.EqualTo(NeonLessonKind.LowJump));
            var at = session.Runner.Body.position;
            float gameTime = Time.time;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(tutorial.RestArmed, Is.True);
            var calibration = input.CurrentCalibration;
            input.ScriptedFlex = Mathf.Lerp(calibration.FlexBaseline, calibration.FlexReference, .98f);
            yield return new WaitForSecondsRealtime(.25f);
            input.ScriptedFlex = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(tutorial.LessonIndex, Is.Zero, "A grab must not dismiss a jump lesson.");
            Assert.That(Time.time, Is.EqualTo(gameTime).Within(.001f));
            Assert.That(session.Runner.Body.position, Is.EqualTo(at));
            Assert.That(session.Router.enabled, Is.False);

            var seen = new HashSet<NeonLessonKind> { tutorial.CurrentKind };
            session.GetComponent<NeonAutopilot>().Active = true;
            deadline = Time.realtimeSinceStartup + 85;
            while (session.State != NeonSessionState.Results && Time.realtimeSinceStartup < deadline)
            {
                if (tutorial.IsPaused) seen.Add(tutorial.CurrentKind);
                yield return null;
            }
            Assert.That(session.State, Is.EqualTo(NeonSessionState.Results),
                $"Stopped at {tutorial.LessonIndex}/{tutorial.Lessons.Length}, {tutorial.CurrentKind}, " +
                $"x={session.Runner.Body.position.x:F2}, y={session.Runner.Body.position.y:F2}, " +
                $"pause={tutorial.IsPaused}, pipeline={input.Pipeline.State}, fault={input.Pipeline.Fault}, deaths={session.Deaths}");
            Assert.That(tutorial.Complete, Is.True);
            Assert.That(seen.Count, Is.EqualTo(8), "Every new mechanic must have a visible freeze lesson.");
            Assert.That(session.Deaths, Is.Zero, "The authored takeoff and release points must be safe.");
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(session.NextScene, Is.EqualTo("Neon_L1_Underground"));
        }
    }
}
