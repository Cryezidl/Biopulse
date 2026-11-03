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
    public sealed class RunnerJumpTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private PhysicsMaterial2D material;
        private long commandId;
        [SetUp]
        public void Setup() => material = new PhysicsMaterial2D { friction = 0, bounciness = 0 };
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear(); Object.Destroy(material); yield return null;
        }
        private void Floor(float width = 30)
        {
            var go = new GameObject("Jump test floor"); objects.Add(go); go.layer = 31;
            go.transform.position = new Vector2(0, -.5f);
            var c = go.AddComponent<BoxCollider2D>(); c.size = new Vector2(width, 1); c.sharedMaterial = material;
        }
        private RunnerMotor2D Runner(float y = .82f, float speed = 0)
        {
            var go = new GameObject("Jump test runner"); objects.Add(go); go.layer = 31;
            go.transform.position = new Vector2(0, y);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.excludeLayers = ~(1 << 31);
            var collider = go.AddComponent<CapsuleCollider2D>(); collider.size = new Vector2(.8f, 1.6f); collider.sharedMaterial = material;
            var motor = go.AddComponent<RunnerMotor2D>(); motor.RunSpeed = speed; return motor;
        }
        private GameCommand Jump(float amplitude) => new GameCommand(++commandId, GameCommandKind.Jump, amplitude, 0, 0);

        [UnityTest]
        public IEnumerator MaximumReplayAmplitudeClearsActualFinalHurdlePrefab()
        {
            #if UNITY_EDITOR
            Floor();
            var hurdleAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Biopulse/Prefabs/FinalHurdle.prefab");
            var runnerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Biopulse/Prefabs/Runner.prefab");
            Assert.That(hurdleAsset, Is.Not.Null); Assert.That(runnerAsset, Is.Not.Null);
            var hurdle = Object.Instantiate(hurdleAsset); objects.Add(hurdle); hurdle.layer = 31;
            var obstacle = hurdle.GetComponent<BoxCollider2D>();
            float height = obstacle.size.y * hurdle.transform.localScale.y;
            hurdle.transform.position = new Vector2(2, height / 2);
            var player = Object.Instantiate(runnerAsset); objects.Add(player); player.layer = 31;
            var motor = player.GetComponent<RunnerMotor2D>();
            motor.Body.excludeLayers = ~(1 << 31);
            // Start right against the obstacle: verifies the user's blocked-at-the-wall case too.
            motor.ResetTo(new Vector2(2 - obstacle.size.x * hurdle.transform.localScale.x / 2 - .41f, .82f));
            var sourceObject = new GameObject("Final hurdle replay source"); objects.Add(sourceObject);
            var driver = sourceObject.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = sourceObject.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Input = driver;
            var samples = new List<EmgSample>();
            for (int i = 0; i <= 300; i++) samples.Add(new EmgSample(i, i * .01, 0, i >= 20 ? 1 : 0));
            var replay = new ReplayEmgSource(samples, () => Time.realtimeSinceStartupAsDouble);
            driver.SetSource(replay, new EmgCalibration(replay.Info.Id));
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(motor.JumpCount, Is.EqualTo(1));
            Assert.That(motor.LastJumpHeight, Is.GreaterThan(height + .1f));
            Assert.That(motor.Body.position.x, Is.GreaterThan(obstacle.bounds.max.x + .5f),
                "The entire character must pass the real final obstacle, not just reach its height.");
            TestContext.WriteLine($"Final height {height:F3}, processed amplitude {motor.LastJumpAmplitude:F3}, peak {motor.LastJumpHeight:F3}, x {motor.Body.position.x:F3}");
            #else
            Assert.Ignore("Saved project prefab regression runs in the Unity Editor.");
            yield break;
            #endif
        }

        [UnityTest]
        public IEnumerator ThreeAmplitudesProduceIncreasingPhysicalJumpHeights()
        {
            Floor(); var motor = Runner(); yield return new WaitForSeconds(.1f);
            float previousHeight = 0;
            foreach (float amplitude in new[] { .35f, .6f, .9f })
            {
                int before = motor.JumpCount;
                Assert.That(motor.QueueCommand(Jump(amplitude)), Is.True);
                for (int frame = 0; frame < 80; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    if (frame > 5 && motor.IsGrounded) break;
                }
                Assert.That(motor.JumpCount, Is.EqualTo(before + 1));
                Assert.That(motor.LastJumpHeight, Is.GreaterThan(previousHeight + .3f));
                previousHeight = motor.LastJumpHeight;
                Assert.That(motor.IsGrounded, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator AirborneCommandExpiresWithoutDoubleJumpOrJumpOnLanding()
        {
            Floor(); var motor = Runner(); yield return new WaitForSeconds(.1f);
            motor.QueueCommand(Jump(.8f)); yield return new WaitForSeconds(.12f);
            Assert.That(motor.IsGrounded, Is.False);
            motor.QueueCommand(Jump(1));
            yield return new WaitForSeconds(1.2f);
            Assert.That(motor.JumpCount, Is.EqualTo(1));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator DuplicateCommandAndResetCannotReplayJump()
        {
            Floor(); var motor = Runner(); yield return new WaitForSeconds(.1f);
            var command = Jump(.6f);
            Assert.That(motor.QueueCommand(command), Is.True);
            Assert.That(motor.QueueCommand(command), Is.False);
            motor.ResetTo(new Vector2(0, .82f));
            yield return new WaitForSeconds(.2f);
            Assert.That(motor.JumpCount, Is.Zero);
            Assert.That(motor.QueueCommand(command), Is.False);
        }

        [UnityTest]
        public IEnumerator SuspensionClearsPendingJump()
        {
            Floor(); var motor = Runner(); yield return new WaitForSeconds(.1f);
            motor.QueueCommand(Jump(.9f)); motor.SetSuspended(true); motor.SetSuspended(false);
            yield return new WaitForSeconds(.2f);
            Assert.That(motor.JumpCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CoyoteJumpWorksJustAfterLeavingPlatform()
        {
            Floor(2); var motor = Runner(speed: 4); yield return new WaitForSeconds(.1f);
            Assert.That(motor.IsGrounded, Is.True);
            for (int i = 0; i < 40 && motor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.False);
            motor.QueueCommand(Jump(.6f));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(motor.JumpCount, Is.EqualTo(1));
            Assert.That(motor.LastJumpUsedCoyote, Is.True);
        }

        [UnityTest]
        public IEnumerator JumpImmediatelyBeforeLandingIsBuffered()
        {
            Floor(); var motor = Runner(y: 1.4f);
            for (int i = 0; i < 50 && motor.Body.position.y > .98f; i++) yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.False);
            motor.QueueCommand(Jump(.6f));
            yield return new WaitForSeconds(.15f);
            Assert.That(motor.JumpCount, Is.EqualTo(1));
            Assert.That(motor.LastJumpUsedBuffer, Is.True);
        }

        [UnityTest]
        public IEnumerator HoldingReplayExtendThroughLandingProducesOneJump()
        {
            Floor(); var motor = Runner();
            var go = new GameObject("Jump test source"); objects.Add(go);
            var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = go.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Input = driver;
            var samples = new List<EmgSample>();
            for (int i = 0; i <= 300; i++) samples.Add(new EmgSample(i, i * .005, 0, i >= 40 ? .6f : 0));
            var source = new ReplayEmgSource(samples, () => Time.realtimeSinceStartupAsDouble);
            driver.SetSource(source, new EmgCalibration(source.Info.Id));
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(motor.JumpCount, Is.EqualTo(1));
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.LastJumpAmplitude, Is.GreaterThan(.25f));
        }

        [UnityTest]
        public IEnumerator InputLossBetweenRenderAndPhysicsCannotExecuteQueuedJump()
        {
            Floor(); var motor = Runner();
            var go = new GameObject("Lost input test source"); objects.Add(go);
            var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = go.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Input = driver;
            yield return new WaitForSeconds(.1f);
            Assert.That(router.TryRoute(new EmgIntent(1, EmgIntentKind.Extend, 0, .9f, 0, 0)), Is.True);
            go.SendMessage("OnApplicationFocus", false);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(motor.JumpCount, Is.Zero);
            Assert.That(motor.IsSuspended, Is.True);
            go.SendMessage("OnApplicationFocus", true);
            yield return null; yield return null;
            driver.Pipeline.Resume();
            yield return new WaitForSeconds(.1f);
            Assert.That(motor.JumpCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FlexCoactivationAndDuplicateIntentCannotCreateExtraJump()
        {
            Floor(); var motor = Runner();
            var go = new GameObject("Router test source"); objects.Add(go);
            var driver = go.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = go.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Input = driver;
            yield return new WaitForSeconds(.1f);
            Assert.That(router.TryRoute(new EmgIntent(1, EmgIntentKind.Flex, .8f, 0, 0, 0)), Is.False);
            Assert.That(router.TryRoute(new EmgIntent(2, EmgIntentKind.Coactivate, .8f, .8f, 0, 0)), Is.False);
            var extend = new EmgIntent(3, EmgIntentKind.Extend, 0, .6f, 0, 0);
            Assert.That(router.TryRoute(extend), Is.True);
            Assert.That(router.TryRoute(extend), Is.False);
            yield return new WaitForSeconds(.1f);
            Assert.That(motor.JumpCount, Is.EqualTo(1));
        }
    }
}
