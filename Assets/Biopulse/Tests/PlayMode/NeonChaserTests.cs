using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Biopulse.Emg;
using Biopulse.Gameplay;
using Biopulse.Input;
using Biopulse.Neon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class NeonChaserTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private RunnerMotor2D runner;
        private NeonLevelSession session;
        private NeonChaser chaser;
        private static readonly MethodInfo Step = typeof(NeonChaser).GetMethod("StepChase", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject New(string name)
        {
            var go = new GameObject(name); objects.Add(go); return go;
        }

        [SetUp]
        public void Setup()
        {
            var player = New("Chaser test runner");
            var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
            player.AddComponent<CapsuleCollider2D>().size = new Vector2(.8f, 1.6f);
            runner = player.AddComponent<RunnerMotor2D>(); runner.RunSpeed = 6.8f;
            body.simulated = false; // Position is advanced explicitly; suspension remains a separate game state.
            session = New("Chaser test session").AddComponent<NeonLevelSession>(); session.enabled = false;
            session.Runner = runner;
            session.Router = New("Chaser test router").AddComponent<RunnerCommandRouter>(); session.Router.enabled = false;
            typeof(NeonLevelSession).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NeonSessionState.Playing });
            chaser = New("Chaser test robot").AddComponent<NeonChaser>(); chaser.enabled = false; chaser.Runner = runner;
            chaser.ResetBehind();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear();
            yield return null;
        }

        private void Tick(float seconds = .02f) => Step.Invoke(chaser, new object[] { session, seconds });
        private void WaitForCatch(float seconds)
        {
            for (int i = 0; i < seconds / .02f && session.State == NeonSessionState.Playing; i++) Tick();
        }

        [Test]
        public void ProximityWarnsThenAttacksWithoutAnyWall()
        {
            chaser.transform.position = new Vector2(-2.5f, 0);
            Tick();
            Assert.That(runner.IsBlocked, Is.False);
            Assert.That(chaser.IsAttacking, Is.True);
            Assert.That(chaser.Lunging, Is.False);
            Assert.That(session.Deaths, Is.Zero);
            WaitForCatch(2);
            Assert.That(session.Deaths, Is.EqualTo(1));
            StringAssert.StartsWith("Mopped@", session.DeathLog[0]);
        }

        [Test]
        public void HangingInOpenAirIsCaught()
        {
            Assert.That(runner.BeginGrab(new Vector2(2, 4)), Is.True);
            Assert.That(runner.IsBlocked, Is.False);
            WaitForCatch(4);
            Assert.That(session.Deaths, Is.EqualTo(1));
        }

        private LockTerminal EnterTerminal()
        {
            var trigger = New("Chaser test terminal").AddComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = Vector2.one * 3;
            var gate = New("Chaser test gate").AddComponent<BoxCollider2D>(); gate.transform.position = Vector2.right * 2;
            var terminal = trigger.gameObject.AddComponent<LockTerminal>(); terminal.Configure(trigger, gate, null);
            var locks = runner.gameObject.AddComponent<RunnerLockController>(); locks.Runner = runner; session.Locks = locks;
            typeof(RunnerMotor2D).GetField("<IsGrounded>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, true);
            Physics2D.SyncTransforms(); locks.RefreshContext();
            Assert.That(locks.Current, Is.EqualTo(terminal));
            Assert.That(runner.IsAtLock, Is.True);
            return terminal;
        }

        [Test]
        public void ClosedTerminalSlowsPursuitButDoesNotStopIt()
        {
            EnterTerminal();
            Vector3 before = chaser.transform.position;
            Tick();
            Assert.That(chaser.MovementSpeed, Is.GreaterThan(0).And.LessThan(chaser.CurrentSpeed));
            Assert.That(chaser.transform.position.x, Is.GreaterThan(before.x));
            WaitForCatch(10);
            Assert.That(session.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void AttackCanKillWhileBothChannelsAreUnlocking()
        {
            var terminal = EnterTerminal();
            terminal.Challenge.Advance(EmgIntentKind.Rest, 0);
            terminal.Challenge.Advance(EmgIntentKind.Coactivate, .1);
            Assert.That(terminal.Challenge.State, Is.EqualTo(LockState.Holding));
            chaser.transform.position = new Vector2(-1.3f, .2f);
            WaitForCatch(1);
            Assert.That(session.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void OpenDoorRestoresSpeedEvenBeforeChannelsAreReleased()
        {
            var terminal = EnterTerminal(); Tick();
            float slow = chaser.MovementSpeed;
            terminal.Challenge.Advance(EmgIntentKind.Rest, 0);
            terminal.Challenge.Advance(EmgIntentKind.Coactivate, .1);
            terminal.Challenge.Advance(EmgIntentKind.Coactivate, .7);
            terminal.ApplyVisuals();
            Assert.That(terminal.IsOpen, Is.True);
            Assert.That(runner.IsAtLock, Is.True);
            Tick();
            Assert.That(chaser.MovementSpeed, Is.GreaterThan(slow * 4));
            chaser.transform.position = new Vector2(-1.3f, .2f);
            WaitForCatch(1);
            Assert.That(session.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void TechnicalPauseFreezesMovementWindupAndSpeedGrowth()
        {
            chaser.transform.position = new Vector2(-2.5f, 0); Tick();
            runner.SetSuspended(true);
            Vector3 before = chaser.transform.position; float seconds = chaser.PursuitSeconds;
            Tick(10);
            Assert.That(chaser.transform.position, Is.EqualTo(before));
            Assert.That(chaser.PursuitSeconds, Is.EqualTo(seconds));
            Assert.That(chaser.Lunging, Is.False);
            Assert.That(session.Deaths, Is.Zero);
            runner.SetSuspended(false); Tick(.05f);
            Assert.That(chaser.Lunging, Is.False);
        }

        [Test]
        public void SignalLossFreezesRobotBeforeMotorHasSuspended()
        {
            session.Input = New("Chaser test input").AddComponent<EmgInputDriver>();
            session.Input.Pipeline.Suspend("test signal loss");
            Vector3 before = chaser.transform.position;
            Tick(10);
            Assert.That(runner.IsSuspended, Is.False);
            Assert.That(chaser.transform.position, Is.EqualTo(before));
            Assert.That(chaser.PursuitSeconds, Is.Zero);
        }

        [Test]
        public void DisabledInputFreezesPursuitEvenWithAValidLastFrame()
        {
            var driver = New("Disabled chaser input").AddComponent<EmgInputDriver>();
            driver.AllowUnfocusedScriptedInput = true;
            var pipeline = driver.Pipeline;
            pipeline.Push(new EmgSample(0, 0, 0, 0), 0);
            pipeline.Push(new EmgSample(1, .01, 0, 0), .01);
            Assert.That(pipeline.Frame.IsValid, Is.True);
            session.Input = driver; driver.enabled = false;
            Vector3 before = chaser.transform.position;
            Tick(10);
            Assert.That(chaser.transform.position, Is.EqualTo(before));
            Assert.That(chaser.PursuitSeconds, Is.Zero);
        }

        [Test]
        public void SpeedRisesGraduallyAndHasACap()
        {
            float initial = chaser.CurrentSpeed;
            runner.Body.position = Vector2.right * 10000;
            Tick(60);
            Assert.That(chaser.CurrentSpeed, Is.EqualTo(initial + .18f).Within(.001f));
            Tick(300);
            Assert.That(chaser.CurrentSpeed, Is.EqualTo(initial + .6f).Within(.001f));
            chaser.ResetBehind();
            Assert.That(chaser.CurrentSpeed, Is.EqualTo(initial));
        }

        [Test]
        public void SmallGapChangeDoesNotCancelAttackButRealEscapeDoes()
        {
            chaser.transform.position = new Vector2(-2.5f, 0); Tick();
            runner.Body.position += Vector2.right * .4f; Tick();
            Assert.That(chaser.IsAttacking, Is.True);
            runner.Body.position += Vector2.right * 8; Tick();
            Assert.That(chaser.IsAttacking, Is.False);
        }

        [Test]
        public void NormalRunningHasTimeToReactInsteadOfAnImmediateForcedDeath()
        {
            for (int i = 0; i < 500; i++)
            {
                runner.Body.position += Vector2.right * (runner.RunSpeed * .02f);
                Tick();
            }
            Assert.That(session.Deaths, Is.Zero);
            Assert.That(chaser.IsAttacking, Is.False);
        }
    }
}
