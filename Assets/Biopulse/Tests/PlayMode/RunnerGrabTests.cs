using System.Collections;
using System.Collections.Generic;
using Biopulse.Emg;
using Biopulse.Gameplay;
using Biopulse.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class RunnerGrabTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private RunnerMotor2D motor;
        private RunnerGrabController grab;
        private GameObject New(string name, Vector2 pos)
        { var go = new GameObject(name); objects.Add(go); go.layer = 31; go.transform.position = pos; return go; }
        [SetUp]
        public void Setup()
        {
            var go = New("Grab runner", Vector2.zero);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.excludeLayers = ~(1 << 31);
            go.AddComponent<CapsuleCollider2D>().size = new Vector2(.8f, 1.6f);
            motor = go.AddComponent<RunnerMotor2D>(); motor.RunSpeed = 0;
            grab = go.AddComponent<RunnerGrabController>(); grab.Runner = motor; grab.QueryLayers = 1 << 31;
        }
        private GrabTarget Target(string id = "target")
        { var t = New(id, new Vector2(2, 2)).AddComponent<GrabTarget>(); t.StableId = id; return t; }
        [UnityTearDown]
        public IEnumerator Cleanup()
        { foreach (var go in objects) if (go != null) Object.Destroy(go); objects.Clear(); yield return null; }

        [UnityTest]
        public IEnumerator WeakReachMissesAndStrongReachAutoGrabs()
        {
            Target(); grab.BeginReach(.35f); grab.Step(.35f, true, .2f);
            Assert.That(motor.IsGrabbed, Is.False);
            grab.BeginReach(.9f); grab.Step(.9f, true, .02f);
            Assert.That(motor.IsGrabbed, Is.True); yield return null;
        }
        [UnityTest]
        public IEnumerator RestPreservesGrabForFiveSecondsAndPauseDoesNotRelease()
        {
            Target(); grab.BeginReach(.9f); grab.Step(.9f, true, .02f);
            Vector2 position = motor.Body.position;
            yield return new WaitForSeconds(5);
            grab.Step(0, true, .02f);
            Assert.That(motor.IsGrabbed, Is.True); Assert.That(motor.Body.position, Is.EqualTo(position));
            motor.SetSuspended(true); grab.Step(0, false, .02f); motor.SetSuspended(false);
            Assert.That(motor.IsGrabbed, Is.True); Assert.That(motor.Body.simulated, Is.False);
            grab.Release(); Assert.That(motor.IsGrabbed, Is.False);
            Assert.That(motor.Body.linearVelocity.y, Is.Zero); Assert.That(motor.JumpCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator SolidWallBlocksGrab()
        {
            Target(); var wall = New("Wall", new Vector2(1, 1)); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 2);
            Physics2D.SyncTransforms(); grab.BeginReach(1); grab.Step(1, true, .2f);
            Assert.That(motor.IsGrabbed, Is.False); yield return null;
        }
        [UnityTest]
        public IEnumerator TargetTieUsesStableIdAndDeletedTargetReleases()
        {
            Target("B"); var a = Target("A"); grab.BeginReach(1); grab.Step(1, true, .02f);
            Assert.That(grab.Target, Is.EqualTo(a));
            Object.Destroy(a.gameObject); yield return null; grab.Step(0, true, .02f);
            Assert.That(motor.IsGrabbed, Is.False);
        }
        [UnityTest]
        public IEnumerator ResetCancelsOldReachAndGrab()
        {
            grab.BeginReach(1); motor.ResetTo(Vector2.zero); Target(); grab.Step(1, true, .02f);
            Assert.That(motor.IsGrabbed, Is.False);
            grab.BeginReach(1); grab.Step(1, true, .02f); Assert.That(motor.IsGrabbed, Is.True);
            motor.ResetTo(Vector2.zero); grab.Step(0, true, .02f); Assert.That(motor.IsGrabbed, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ReplayFlexThenRestThenExtendReleasesWithoutJump()
        {
            Target(); var go = New("Grab source", new Vector2(0, -10));
            var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = go.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Grab = grab; router.Input = driver;
            var samples = new List<EmgSample>();
            for (int i = 0; i <= 240; i++) samples.Add(new EmgSample(i, i * .005,
                i >= 40 && i < 65 ? .9f : 0, i >= 140 ? .9f : 0));
            var replay = new ReplayEmgSource(samples, () => Time.realtimeSinceStartupAsDouble);
            driver.SetSource(replay, new EmgCalibration(replay.Info.Id));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(motor.IsGrabbed, Is.True); Assert.That(driver.Pipeline.State, Is.EqualTo(EmgIntentKind.Rest));
            yield return new WaitForSecondsRealtime(.55f);
            Assert.That(motor.IsGrabbed, Is.False); Assert.That(motor.JumpCount, Is.Zero);
            Assert.That(motor.Body.linearVelocity.y, Is.Zero);
        }
    }
}
