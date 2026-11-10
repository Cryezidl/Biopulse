using System.Collections;
using System.Linq;
using System.Reflection;
using Biopulse.Neon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class NeonCoinTests
    {
        private NeonLevelSession session;
        private bool hadBalance;
        private int balance, seed;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void BackupBalance()
        {
            hadBalance = PlayerPrefs.HasKey(NeonProgress.CoinsKey);
            balance = NeonProgress.TotalCoins;
        }

        private void SeedScene(Scene scene, LoadSceneMode mode)
        {
            Object.FindFirstObjectByType<NeonCoinDirector>().Seed = seed;
            var endless = Object.FindFirstObjectByType<NeonEndlessRooftops>();
            if (endless != null) endless.Seed = 947;
        }

        private IEnumerator Load(string scene, int coinSeed = 321)
        {
            seed = coinSeed;
            SceneManager.sceneLoaded += SeedScene;
            SceneManager.LoadScene(scene);
            yield return null;
            SceneManager.sceneLoaded -= SeedScene;
            yield return null;
            session = NeonLevelSession.Current;
            session.enabled = false;
            session.Router.enabled = false;
            session.Chaser.enabled = false;
            var endless = Object.FindFirstObjectByType<NeonEndlessRooftops>();
            if (endless != null) endless.enabled = false;
            session.Runner.SetSuspended(true);
            Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= SeedScene;
            if (hadBalance) PlayerPrefs.SetInt(NeonProgress.CoinsKey, balance);
            else PlayerPrefs.DeleteKey(NeonProgress.CoinsKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Coin test cleanup"));
            yield return SceneManager.UnloadSceneAsync(old);
        }

        [UnityTest]
        public IEnumerator AllLevelsSpawnSafeRowsOfAtMostThreeWithMediumSpacing()
        {
            foreach (string name in new[] { "Neon_L1_Underground", "Neon_L2_SurfaceLab", "Neon_L3_Rooftops" })
            {
                yield return Load(name);
                var coins = Object.FindObjectsByType<NeonCoin>(FindObjectsSortMode.None).OrderBy(c => c.transform.position.x).ToArray();
                Assert.That(coins.Length, Is.GreaterThan(0), name);
                int row = 0;
                float last = float.NegativeInfinity;
                foreach (var coin in coins)
                {
                    float x = coin.transform.position.x;
                    if (x - last > 2)
                    {
                        if (!float.IsNegativeInfinity(last)) Assert.That(x - last, Is.GreaterThanOrEqualTo(15.9f));
                        row = 0;
                    }
                    Assert.That(++row, Is.LessThanOrEqualTo(3)); last = x;
                    if (!session.IsEndless) Assert.That(x, Is.LessThan(session.FinishX));
                    foreach (var overlap in Physics2D.OverlapBoxAll(coin.transform.position, new Vector2(.88f, 1.7f), 0))
                        Assert.That(overlap.isTrigger && overlap.GetComponentInParent<NeonHazard>() == null, Is.True,
                            $"{name}: coin {coin.transform.position} intersects {overlap.name}");
                }
                float length = session.IsEndless ? Object.FindFirstObjectByType<NeonEndlessRooftops>().GeneratedThrough : session.FinishX;
                Assert.That(coins.Length, Is.LessThan(length / 4), "Reward should not form a continuous carpet.");
            }
        }

        [UnityTest]
        public IEnumerator RegularEmgPlayCollectsCoinsWithoutTeleporting()
        {
            yield return Load("Neon_L3_Rooftops");
            session.Input.ApplyCalibration(new Biopulse.Emg.EmgCalibration(session.Input.CurrentCalibration.SourceId, 0, .6f, 0, .6f));
            session.Input.AllowUnfocusedScriptedInput = true;
            session.GetComponent<NeonAutopilot>().Active = true;
            session.enabled = true;
            session.Chaser.enabled = true;
            Object.FindFirstObjectByType<NeonEndlessRooftops>().enabled = true;
            float deadline = Time.realtimeSinceStartup + 22;
            while (session.Distance < 75 && session.Deaths == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            session.enabled = false;
            Assert.That(session.Deaths, Is.Zero, string.Join(", ", session.DeathLog));
            Assert.That(session.Distance, Is.GreaterThanOrEqualTo(75));
            Assert.That(session.CoinsCollected, Is.GreaterThan(0));
            Assert.That(NeonProgress.TotalCoins, Is.EqualTo(balance + session.CoinsCollected));
        }

        [UnityTest]
        public IEnumerator DifferentSeedsChangePlacement()
        {
            yield return Load("Neon_L1_Underground", 17);
            var first = Object.FindObjectsByType<NeonCoin>(FindObjectsSortMode.None).Select(c => c.transform.position).OrderBy(p => p.x).ToArray();
            yield return Load("Neon_L1_Underground", 29);
            var second = Object.FindObjectsByType<NeonCoin>(FindObjectsSortMode.None).Select(c => c.transform.position).OrderBy(p => p.x).ToArray();
            Assert.That(first.SequenceEqual(second), Is.False);
        }

        [UnityTest]
        public IEnumerator TriggerAwardsOnceAndDeathDoesNotRestoreCollectedCoins()
        {
            yield return Load("Neon_L1_Underground");
            var coin = Object.FindObjectsByType<NeonCoin>(FindObjectsSortMode.None).First();
            typeof(NeonLevelSession).GetMethod("SetState", Private).Invoke(session, new object[] { NeonSessionState.Playing });
            session.Runner.RunSpeed = 0; session.Runner.Body.gravityScale = 0;
            session.Runner.ResetTo(coin.transform.position); session.Runner.SetSuspended(false);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(coin.Collected, Is.True);
            Assert.That(session.CoinsCollected, Is.EqualTo(1));
            Assert.That(session.Score, Is.EqualTo(25));
            Assert.That(NeonProgress.TotalCoins, Is.EqualTo(balance + 1));
            Assert.That(coin.TryCollect(session.Runner), Is.False);
            session.Kill(NeonDeath.Mopped);
            typeof(NeonLevelSession).GetMethod("Respawn", Private).Invoke(session, null);
            Assert.That(coin.gameObject.activeSelf, Is.False);
            Assert.That(coin.TryCollect(session.Runner), Is.False);
            Assert.That(NeonProgress.TotalCoins, Is.EqualTo(balance + 1));
        }

        [UnityTest]
        public IEnumerator IntroAndTechnicalPauseCannotAwardCoins()
        {
            yield return Load("Neon_L1_Underground");
            var coin = Object.FindFirstObjectByType<NeonCoin>();
            Assert.That(coin.TryCollect(session.Runner), Is.False);
            typeof(NeonLevelSession).GetMethod("SetState", Private).Invoke(session, new object[] { NeonSessionState.Playing });
            Assert.That(coin.TryCollect(session.Runner), Is.False); // Motor remains technically suspended.
            Assert.That(session.CoinsCollected, Is.Zero);
            Assert.That(NeonProgress.TotalCoins, Is.EqualTo(balance));
        }

        [UnityTest]
        public IEnumerator EndlessCoinPoolsReuseObjectsAndFollowOriginShifts()
        {
            yield return Load("Neon_L3_Rooftops");
            var endless = Object.FindFirstObjectByType<NeonEndlessRooftops>();
            var director = session.GetComponent<NeonCoinDirector>();
            var chunk = endless.GetComponentsInChildren<NeonRooftopChunk>().First(c => c.Coins.Routes.Length > 0);
            var cursor = typeof(NeonCoinDirector).GetField("<NextGroupX>k__BackingField", Private);
            int created = 0;
            for (int i = 0; i < 50; i++)
            {
                cursor.SetValue(director, chunk.transform.position.x + 3);
                director.Populate(chunk.Coins);
                if (i > 5) Assert.That(chunk.Coins.PooledCoinCount, Is.EqualTo(created));
                created = chunk.Coins.PooledCoinCount;
                Assert.That(created, Is.LessThanOrEqualTo(chunk.Coins.Routes.Length * 3));
            }
            var coin = Object.FindFirstObjectByType<NeonCoin>();
            float before = coin.transform.position.x, next = director.NextGroupX;
            typeof(NeonEndlessRooftops).GetMethod("Rebase", Private).Invoke(endless, new object[] { 1500f });
            Assert.That(coin.transform.position.x, Is.EqualTo(before - 1500).Within(.01f));
            Assert.That(director.NextGroupX, Is.EqualTo(next - 1500).Within(.01f));
        }
    }
}
