using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Biopulse.Gameplay;
using Biopulse.Neon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class EndlessRooftopsTests
    {
        private NeonLevelSession session;
        private NeonEndlessRooftops endless;
        private bool hadCoinBalance;
        private int coinBalance;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator LoadRooftops()
        {
            hadCoinBalance = PlayerPrefs.HasKey(NeonProgress.CoinsKey);
            coinBalance = NeonProgress.TotalCoins;
            SceneManager.sceneLoaded += SeedRooftops;
            SceneManager.LoadScene("Neon_L3_Rooftops");
            yield return null;
            SceneManager.sceneLoaded -= SeedRooftops;
            yield return null;
            session = NeonLevelSession.Current;
            endless = Object.FindFirstObjectByType<NeonEndlessRooftops>();
            session.enabled = false;
            endless.enabled = false;
            session.Router.enabled = false;
            session.Runner.SetSuspended(true);
        }

        private static void SeedRooftops(Scene scene, LoadSceneMode mode)
        {
            var generator = Object.FindFirstObjectByType<NeonEndlessRooftops>();
            if (generator != null) generator.Seed = 947;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (hadCoinBalance) PlayerPrefs.SetInt(NeonProgress.CoinsKey, coinBalance);
            else PlayerPrefs.DeleteKey(NeonProgress.CoinsKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
            var old = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Endless test cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(old);
        }

        [Test]
        public void RooftopsUseOnlyEndlessPatternsWithoutStopsOrFinish()
        {
            Assert.That(session.IsEndless, Is.True);
            Assert.That(session.CheckpointXs, Is.Empty);
            Assert.That(Object.FindObjectsByType<NeonCheckpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<NeonFinish>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<LockTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(endless.Templates.Length, Is.EqualTo(38));
            Assert.That(endless.Templates.All(t => !t.gameObject.activeInHierarchy && t.Length >= 8), Is.True);
            Assert.That(endless.GeneratedThrough, Is.GreaterThanOrEqualTo(110));
            foreach (var layer in Object.FindObjectsByType<NeonParallax>(FindObjectsSortMode.None).Where(l => l.RepeatWidth > 0))
                Assert.That(layer.RepeatWidth * layer.transform.childCount, Is.GreaterThan(60), layer.name);
        }

        [Test]
        public void OpeningHasLasersAndReachableGrabCubes()
        {
            var chunks = endless.GetComponentsInChildren<NeonRooftopChunk>().OrderBy(c => c.Sequence).ToArray();
            Assert.That(chunks[1].Kind, Is.EqualTo(NeonRooftopKind.Laser));
            Assert.That(chunks[1].GetComponentsInChildren<NeonHazard>().Any(h => h.Kind == NeonDeath.Laser), Is.True);
            Assert.That(chunks[2].Kind, Is.EqualTo(NeonRooftopKind.Grab));
            Assert.That(chunks[2].Targets, Is.Not.Empty);
            var target = chunks[2].Targets[0];
            Assert.That(target.transform.position.x, Is.LessThan(60));
            Assert.That(target.transform.Find("Grab cube body"), Is.Not.Null);
            // The first cube can be reached from the takeoff edge with the existing six-unit arm.
            var grabZone = chunks[2].Zones.First(z => z.Challenge == NeonChallenge.Grab);
            float edge = grabZone.transform.position.x - grabZone.GetComponent<BoxCollider2D>().size.x / 2 + 4.5f;
            Assert.That(Vector2.Distance(new Vector2(edge - .2f, .85f), target.transform.position), Is.LessThan(6));
            foreach (var template in endless.Templates)
            {
                if (template.Kind == NeonRooftopKind.Grab) Assert.That(template.Targets, Is.Not.Empty, template.name);
                if (template.Kind == NeonRooftopKind.Laser)
                    Assert.That(template.GetComponentsInChildren<NeonHazard>(true).Any(h => h.Kind == NeonDeath.Laser), Is.True, template.name);
            }
        }

        [Test]
        public void EarlyRunShowsAllEligibleVariantsIncludingTripleAndHeightChangingGrabs()
        {
            var maintain = typeof(NeonEndlessRooftops).GetMethod("Maintain", Private);
            var seen = new Dictionary<int, NeonRooftopChunk>();
            bool triple = false, changingHeight = false, roofGrabs = false, threeHurdles = false;
            int expectedVariants = endless.Templates.Count(t => t.MinimumTier == 0);
            var names = new HashSet<string>();
            for (int step = 0; step < 100; step++)
            {
                // Remain in tier zero: variety must not depend on reaching a later difficulty tier.
                session.Runner.ResetTo(new Vector2(step * 13, .85f));
                session.Runner.SetSuspended(true);
                maintain.Invoke(endless, null);
                foreach (var chunk in endless.GetComponentsInChildren<NeonRooftopChunk>())
                {
                    if (seen.ContainsKey(chunk.Sequence)) continue;
                    seen.Add(chunk.Sequence, chunk); names.Add(chunk.name);
                    triple |= chunk.Targets.Length >= 3;
                    changingHeight |= chunk.Targets.Length >= 3 && chunk.Targets.Select(t => t.transform.position.y).Distinct().Count() > 1;
                    roofGrabs |= chunk.transform.Find("Grab points/Cubes above rooftop") != null;
                    threeHurdles |= chunk.GetComponentsInChildren<NeonHazard>().Any(h => h.name == "Laser ceiling") && chunk.Zones.Length >= 3;
                }
            }
            Assert.That(names.Count, Is.EqualTo(expectedVariants));
            Assert.That(triple && changingHeight && roofGrabs && threeHurdles, Is.True);
        }

        [Test]
        public void LongRunRecyclesChunksAndKeepsTargetCoordinatesAcrossOriginShifts()
        {
            var maintain = typeof(NeonEndlessRooftops).GetMethod("Maintain", Private);
            var rebase = typeof(NeonEndlessRooftops).GetMethod("Rebase", Private);
            var distance = typeof(NeonLevelSession).GetField("<Distance>k__BackingField", Private);
            float x = 0;
            var generated = new Dictionary<int, NeonRooftopKind>();
            for (int step = 0; step < 1500; step++)
            {
                x += 23;
                distance.SetValue(session, step * 23f);
                session.Runner.ResetTo(new Vector2(x, .85f));
                session.Runner.SetSuspended(true);
                maintain.Invoke(endless, null);
                Assert.That(endless.GeneratedThrough, Is.GreaterThanOrEqualTo(x + endless.Ahead));
                Assert.That(endless.ActiveChunkCount, Is.LessThanOrEqualTo(16));
                if (x > 2000)
                {
                    var zone = Object.FindObjectsByType<NeonTimingZone>(FindObjectsSortMode.None).First();
                    float relativeTarget = zone.TargetX - zone.transform.position.x;
                    rebase.Invoke(endless, new object[] { 1500f });
                    x -= 1500;
                    Assert.That(session.Runner.Body.position.x, Is.EqualTo(x).Within(.01f));
                    Assert.That(zone.TargetX - zone.transform.position.x, Is.EqualTo(relativeTarget).Within(.01f));
                }
                var chunks = endless.GetComponentsInChildren<NeonRooftopChunk>().OrderBy(c => c.transform.position.x).ToArray();
                foreach (var chunk in chunks)
                {
                    generated[chunk.Sequence] = chunk.Kind;
                }
                for (int i = 1; i < chunks.Length; i++)
                    Assert.That(chunks[i].transform.position.x, Is.EqualTo(chunks[i - 1].EndX).Within(.01f));
                var ids = Object.FindObjectsByType<GrabTarget>(FindObjectsSortMode.None).Select(t => t.StableId).ToArray();
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            }
            Assert.That(endless.CreatedChunkCount, Is.LessThanOrEqualTo(endless.Templates.Length * 4));
            for (int start = 0; start + 5 < generated.Count; start += 6)
            {
                var cycle = Enumerable.Range(start, 6).Select(i => generated[i]).ToArray();
                Assert.That(cycle.Count(k => k == NeonRooftopKind.Rest), Is.EqualTo(1));
                Assert.That(cycle.Count(k => k == NeonRooftopKind.Laser), Is.EqualTo(2));
                Assert.That(cycle.Count(k => k == NeonRooftopKind.Grab), Is.EqualTo(2));
                Assert.That(cycle.Count(k => k == NeonRooftopKind.Jump), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator FirstCeilingAndCubesArePlayableWithCalibratedInput()
        {
            const string key = "biopulse.rooftops.endless";
            string[] suffixes = { ".distance", ".score", ".lastSeconds" };
            bool[] existed = suffixes.Select(s => PlayerPrefs.HasKey(key + s)).ToArray();
            float[] values = { PlayerPrefs.GetFloat(key + suffixes[0]), PlayerPrefs.GetInt(key + suffixes[1]), PlayerPrefs.GetFloat(key + suffixes[2]) };
            try
            {
                session.Input.ApplyCalibration(new Biopulse.Emg.EmgCalibration(session.Input.CurrentCalibration.SourceId, 0, .6f, 0, .6f));
                session.Input.AllowUnfocusedScriptedInput = true;
                session.GetComponent<NeonAutopilot>().Active = true;
                session.enabled = endless.enabled = true;
                bool grabbed = false;
                var grabbedTargets = new HashSet<string>();
                float deadline = Time.realtimeSinceStartup + 22;
                while (session.Distance < 75 && session.State != NeonSessionState.Results && Time.realtimeSinceStartup < deadline)
                {
                    grabbed |= session.Runner.IsGrabbed;
                    if (session.Runner.IsGrabbed && session.Grab.Target != null) grabbedTargets.Add(session.Grab.Target.StableId);
                    yield return null;
                }
                Assert.That(session.Deaths, Is.Zero, string.Join(", ", session.DeathLog));
                Assert.That(session.Distance, Is.GreaterThanOrEqualTo(75));
                Assert.That(session.Runner.JumpCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(grabbed, Is.True, "The opening must exercise grabbing, not only running/jumping.");
                Assert.That(grabbedTargets.Count, Is.GreaterThanOrEqualTo(2), "The triple-cube opening must allow a chain of grabs.");
            }
            finally
            {
                for (int i = 0; i < suffixes.Length; i++)
                {
                    if (!existed[i]) PlayerPrefs.DeleteKey(key + suffixes[i]);
                    else if (i == 1) PlayerPrefs.SetInt(key + suffixes[i], (int)values[i]);
                    else PlayerPrefs.SetFloat(key + suffixes[i], values[i]);
                }
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator ResultsCanRestartAttemptAndReturnToMenu()
        {
            var confirm = typeof(NeonLevelSession).GetMethod("ConfirmResults", Private);
            var old = session;
            confirm.Invoke(session, null); // First action is restart in endless mode.
            yield return null;
            yield return null;
            session = NeonLevelSession.Current;
            Assert.That(session, Is.Not.Null);
            Assert.That(session != old, Is.True);
            Assert.That(session.Distance, Is.Zero);
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.State, Is.EqualTo(NeonSessionState.Intro));
            typeof(NeonLevelSession).GetField("<ResultsFocus>k__BackingField", Private).SetValue(session, 1);
            confirm.Invoke(session, null);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Neon_Menu"));
            Assert.That(NeonLevelSession.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator DeathEndsAttemptAndRecordsSeparateEndlessBest()
        {
            const string key = "biopulse.rooftops.endless";
            string[] suffixes = { ".distance", ".score", ".lastSeconds" };
            bool[] existed = suffixes.Select(s => PlayerPrefs.HasKey(key + s)).ToArray();
            float[] values = { PlayerPrefs.GetFloat(key + suffixes[0]), PlayerPrefs.GetInt(key + suffixes[1]), PlayerPrefs.GetFloat(key + suffixes[2]) };
            int legacyBest = NeonProgress.Best(3);
            try
            {
                typeof(NeonLevelSession).GetMethod("SetState", Private).Invoke(session, new object[] { NeonSessionState.Playing });
                typeof(NeonLevelSession).GetField("<Distance>k__BackingField", Private).SetValue(session, 1234f);
                var before = session.Runner.Body.position;
                session.enabled = true;
                session.Kill(NeonDeath.Fall);
                yield return new WaitForSeconds(1.3f);
                Assert.That(session.State, Is.EqualTo(NeonSessionState.Results));
                Assert.That(session.Deaths, Is.EqualTo(1));
                Assert.That(session.Runner.Body.position, Is.EqualTo(before));
                Assert.That(session.Runner.IsSuspended, Is.True);
                Assert.That(NeonProgress.BestEndlessDistance, Is.GreaterThanOrEqualTo(1234));
                Assert.That(NeonProgress.Best(3), Is.EqualTo(legacyBest));
            }
            finally
            {
                for (int i = 0; i < suffixes.Length; i++)
                {
                    if (!existed[i]) PlayerPrefs.DeleteKey(key + suffixes[i]);
                    else if (i == 1) PlayerPrefs.SetInt(key + suffixes[i], (int)values[i]);
                    else PlayerPrefs.SetFloat(key + suffixes[i], values[i]);
                }
                PlayerPrefs.Save();
            }
        }
    }
}
