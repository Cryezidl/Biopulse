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
        private PhysicsMaterial2D material;
        private GameObject New(string name, Vector2 pos)
        { var go = new GameObject(name); objects.Add(go); go.layer = 31; go.transform.position = pos; return go; }
        [SetUp]
        public void Setup()
        {
            material = new PhysicsMaterial2D { friction = 0, bounciness = 0 };
            var go = New("Grab runner", Vector2.zero);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.excludeLayers = ~(1 << 31);
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(.8f, 1.6f); collider.sharedMaterial = material;
            motor = go.AddComponent<RunnerMotor2D>(); motor.RunSpeed = 0;
            grab = go.AddComponent<RunnerGrabController>(); grab.Runner = motor; grab.QueryLayers = 1 << 31;
        }
        private GrabTarget Target(string id = "target")
        { var t = New(id, new Vector2(2, 2)).AddComponent<GrabTarget>(); t.StableId = id; return t; }
        [UnityTearDown]
        public IEnumerator Cleanup()
        { foreach (var go in objects) if (go != null) Object.Destroy(go); objects.Clear(); Object.Destroy(material); yield return null; }

        [UnityTest]
        public IEnumerator AirbornePlayerGrabsTargetAtSameHeight()
        {
            motor.ResetTo(new Vector2(0,3));
            var target = Target(); target.transform.position = new Vector2(2,3);
            grab.BeginReach(.6f); grab.Step(.6f,true,.02f);
            Assert.That(motor.IsGrounded,Is.False); Assert.That(motor.IsGrabbed,Is.True);
            Assert.That(grab.Target,Is.SameAs(target)); yield return null;
        }
        [UnityTest]
        public IEnumerator NearestForwardTargetWinsOverDiagonalPreferenceAndBehindTarget()
        {
            var diagonal = Target("diagonal"); diagonal.transform.position = new Vector2(2,2);
            var nearest = Target("nearest"); nearest.transform.position = new Vector2(1.5f,-.2f);
            var behind = Target("behind"); behind.transform.position = new Vector2(-.1f,0);
            grab.BeginReach(1); grab.Step(1,true,.02f);
            Assert.That(grab.Target,Is.SameAs(nearest)); yield return null;
        }
        [UnityTest]
        public IEnumerator WeakAttemptPointsAtTargetWithoutExceedingAmplitudeRange()
        {
            var target = Target(); target.transform.position = new Vector2(3,-1);
            var line = motor.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = true; grab.Arm = line;
            grab.BeginReach(.2f); grab.Step(.2f,true,.02f);
            Assert.That(grab.AimTarget,Is.SameAs(target)); Assert.That(motor.IsGrabbed,Is.False);
            Vector2 end = line.GetPosition(1);
            Assert.That(end.magnitude,Is.EqualTo(grab.CurrentReach).Within(.001));
            Assert.That(Vector2.Angle(end,new Vector2(3,-1)),Is.LessThan(.01));
            grab.Step(1,true,.02f); Assert.That(motor.IsGrabbed,Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator OccludedNearestTargetDoesNotHideVisibleTarget()
        {
            var hidden = Target("hidden"); hidden.transform.position = new Vector2(2,0);
            var visible = Target("visible"); visible.transform.position = new Vector2(2,2);
            var wall = New("Short wall",new Vector2(1,0)); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f,.4f);
            Physics2D.SyncTransforms(); grab.BeginReach(1); grab.Step(1,true,.02f);
            Assert.That(grab.Target,Is.SameAs(visible)); yield return null;
        }
        [UnityTest]
        public IEnumerator GrabDoesNotSwitchToANewerCloserTarget()
        {
            var original = Target("original"); grab.BeginReach(1); grab.Step(1,true,.02f);
            var closer = Target("closer"); closer.transform.position = new Vector2(.5f,0);
            grab.Step(1,true,.02f); Assert.That(grab.Target,Is.SameAs(original)); yield return null;
        }
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
            Assert.That(motor.IsGrabbed, Is.True);
            Assert.That(motor.Body.simulated, Is.True);
            Assert.That(Vector2.Distance(motor.Body.position, position), Is.GreaterThan(.2f));
            motor.SetSuspended(true); grab.Step(0, false, .02f);
            Assert.That(motor.Body.simulated, Is.False);
            motor.SetSuspended(false);
            Assert.That(motor.IsGrabbed, Is.True); Assert.That(motor.Body.simulated, Is.True);
            Vector2 velocity = motor.Body.linearVelocity;
            grab.Release(); Assert.That(motor.IsGrabbed, Is.False);
            Assert.That(motor.Body.linearVelocity, Is.EqualTo(velocity)); Assert.That(motor.JumpCount, Is.Zero);
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
            Assert.That(motor.GetComponent<DistanceJoint2D>().enabled, Is.False);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.Body.position, Is.EqualTo(Vector2.zero));
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
        }

        [UnityTest]
        public IEnumerator CapturePreservesVelocityAndPullShortensRope()
        {
            var target = Target();
            motor.Body.linearVelocity = new Vector2(4, 1);
            grab.BeginReach(1); grab.Step(1, true, .02f);
            float initialLength = motor.GrabLength;
            Assert.That(motor.Body.simulated, Is.True);
            Assert.That(motor.Body.linearVelocity, Is.EqualTo(new Vector2(4, 1)));
            yield return new WaitForSeconds(.3f);
            Assert.That(motor.GrabLength, Is.LessThan(initialLength - .5f));
            Assert.That(motor.Body.position.x, Is.GreaterThan(.3f));
            Assert.That(Vector2.Distance(motor.Body.position, target.transform.position),
                Is.LessThanOrEqualTo(motor.GrabLength + .08f));
            Assert.That(motor.Body.rotation, Is.EqualTo(0).Within(.001));
        }

        [UnityTest]
        public IEnumerator GravityAndMomentumSwingPastAnchorAndBackWithoutInput()
        {
            var target = Target();
            motor.Body.gravityScale = 3;
            motor.Body.linearVelocity = new Vector2(4, 0);
            grab.BeginReach(1); grab.Step(1, true, .02f);
            float furthestRight = float.NegativeInfinity, mostNegativeSpeed = 0;
            for (int i = 0; i < 140; i++)
            {
                yield return new WaitForFixedUpdate();
                grab.Step(0, true, Time.fixedDeltaTime);
                furthestRight = Mathf.Max(furthestRight, motor.Body.position.x - target.transform.position.x);
                mostNegativeSpeed = Mathf.Min(mostNegativeSpeed, motor.Body.linearVelocity.x);
            }
            Assert.That(motor.IsGrabbed, Is.True);
            Assert.That(furthestRight, Is.GreaterThan(.3f), "Swing must pass under and beyond the anchor.");
            Assert.That(mostNegativeSpeed, Is.LessThan(-.3f), "Gravity must allow the return swing.");
        }

        [UnityTest]
        public IEnumerator ReleasePreservesBothVelocityComponentsThroughoutFlight()
        {
            Target(); grab.BeginReach(1); grab.Step(1, true, .02f);
            motor.RunSpeed = 4;
            motor.Body.linearVelocity = new Vector2(8, -2);
            Vector2 position = motor.Body.position;
            grab.Release();
            Assert.That(motor.Body.linearVelocity, Is.EqualTo(new Vector2(8, -2)));
            Assert.That(motor.GetComponent<DistanceJoint2D>().enabled, Is.False);
            yield return new WaitForSeconds(.12f);
            Assert.That(motor.Body.linearVelocity.x, Is.EqualTo(8).Within(.01));
            Assert.That(motor.Body.linearVelocity.y, Is.EqualTo(-2).Within(.01));
            Assert.That(motor.Body.position.x, Is.GreaterThan(position.x + .6f));
            Assert.That(motor.JumpCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LandingAfterReleaseRestoresAutorun()
        {
            var floor = New("Landing floor", new Vector2(10, -.5f));
            var collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(40, 1); collider.sharedMaterial = material;
            motor.ResetTo(new Vector2(0, 2)); motor.RunSpeed = 4; motor.Body.gravityScale = 3;
            var target = Target(); target.transform.position = new Vector2(2, 4);
            Physics2D.SyncTransforms();
            grab.BeginReach(1); grab.Step(1, true, .02f);
            motor.Body.linearVelocity = new Vector2(8, 0); grab.Release();
            yield return new WaitForSeconds(.7f);
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.Body.linearVelocity.x, Is.EqualTo(4).Within(.05));
            Assert.That(motor.JumpCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PauseFreezesSwingAndRetractionThenResumesSameVelocity()
        {
            Target(); motor.Body.gravityScale = 3; motor.Body.linearVelocity = new Vector2(4, 0);
            grab.BeginReach(1); grab.Step(1, true, .02f);
            yield return new WaitForSeconds(.12f);
            Vector2 position = motor.Body.position, velocity = motor.Body.linearVelocity;
            float length = motor.GrabLength;
            motor.SetSuspended(true);
            yield return new WaitForSeconds(.2f);
            grab.Step(0, false, .02f);
            Assert.That(motor.Body.position, Is.EqualTo(position));
            Assert.That(motor.GrabLength, Is.EqualTo(length));
            Assert.That(motor.IsGrabbed, Is.True);
            motor.SetSuspended(false);
            Assert.That(motor.Body.simulated, Is.True);
            Assert.That(motor.Body.linearVelocity, Is.EqualTo(velocity));
            yield return new WaitForSeconds(.1f);
            Assert.That(Vector2.Distance(motor.Body.position, position), Is.GreaterThan(.05f));
        }

        [UnityTest]
        public IEnumerator TargetMovementUpdatesAnchorAndNewOccluderReleasesSafely()
        {
            var target = Target(); grab.BeginReach(1); grab.Step(1, true, .02f);
            target.transform.position = new Vector2(2.1f, 2.1f);
            grab.Step(0, true, .02f);
            Assert.That(motor.GetComponent<DistanceJoint2D>().connectedAnchor,
                Is.EqualTo((Vector2)target.transform.position));
            var wall = New("New occluder", new Vector2(1, 1));
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 2);
            Physics2D.SyncTransforms(); grab.Step(0, true, .02f);
            Assert.That(motor.IsGrabbed, Is.False);
            Assert.That(motor.Body.simulated, Is.True);
            Assert.That(motor.GetComponent<DistanceJoint2D>().enabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedCaptureReusesJointAndDisableHidesArm()
        {
            Target();
            var line = motor.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = true; grab.Arm = line;
            for (int i = 0; i < 3; i++)
            {
                grab.BeginReach(1); grab.Step(1, true, .02f);
                Assert.That(motor.IsGrabbed, Is.True); grab.Release();
            }
            Assert.That(motor.GetComponents<DistanceJoint2D>().Length, Is.EqualTo(1));
            grab.BeginReach(1); grab.Step(1, true, .02f);
            grab.enabled = false;
            Assert.That(motor.IsGrabbed, Is.False);
            Assert.That(line.enabled, Is.False);
            Assert.That(motor.GetComponent<DistanceJoint2D>().enabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwingCollidesWithFloorWithoutSinkingOrBreakingGrab()
        {
            var floor = New("Swing floor", new Vector2(5, -1.3f));
            var collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(30, 1); collider.sharedMaterial = material;
            Target(); motor.Body.gravityScale = 3; motor.Body.linearVelocity = new Vector2(8, 0);
            Physics2D.SyncTransforms();
            grab.BeginReach(1); grab.Step(1, true, .02f);
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate(); grab.Step(0, true, Time.fixedDeltaTime);
                Assert.That(motor.Body.position.y, Is.GreaterThan(-.04f));
            }
            Assert.That(motor.IsGrabbed, Is.True);
        }
    }
}
