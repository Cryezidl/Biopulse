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
    public sealed class RunnerLockTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private RunnerMotor2D motor;
        private RunnerLockController controller;
        private EmgInputDriver driver;
        private LockTerminal terminal;
        private Collider2D gate;
        private GameObject New(string name, Vector2 position)
        { var go = new GameObject(name); objects.Add(go); go.layer = 31; go.transform.position = position; return go; }
        [SetUp]
        public void Setup()
        {
            var floor = New("Lock floor", new Vector2(0,-.5f)); floor.AddComponent<BoxCollider2D>().size = new Vector2(30,1);
            var player = New("Lock player", new Vector2(1.5f,.82f));
            var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.excludeLayers = ~(1<<31);
            player.AddComponent<CapsuleCollider2D>().size = new Vector2(.8f,1.6f);
            motor = player.AddComponent<RunnerMotor2D>();
            controller = player.AddComponent<RunnerLockController>(); controller.Runner = motor; controller.TerminalLayers = 1<<31;
            var zone = New("Lock zone", new Vector2(2,1)); var trigger = zone.AddComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = new Vector2(2,3);
            gate = New("Gate", new Vector2(3,1.5f)).AddComponent<BoxCollider2D>(); ((BoxCollider2D)gate).size = new Vector2(.2f,3);
            terminal = zone.AddComponent<LockTerminal>(); terminal.Configure(trigger,gate,null);
            var services = New("Lock source", Vector2.zero); driver = services.AddComponent<EmgInputDriver>(); driver.ShowDiagnostics = false;
            var router = services.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Terminal = controller; router.Input = driver;
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { foreach(var go in objects) if(go!=null) Object.Destroy(go); objects.Clear(); yield return null; }
        private void Replay(bool combination)
        {
            var frames = new List<EmgSample>();
            for(int i=0;i<=400;i++) frames.Add(new EmgSample(i,i*.005,
                combination && i>=50 && i<210 ? .8f:0, i>=50 && i<240 ? .8f:0));
            var source = new ReplayEmgSource(frames,()=>Time.realtimeSinceStartupAsDouble);
            driver.SetSource(source,new EmgCalibration(source.Info.Id));
        }
        [UnityTest]
        public IEnumerator FlexMayPrecedeExtendAtTerminal() => StaggeredCombination(true);
        [UnityTest]
        public IEnumerator ExtendMayPrecedeFlexAtTerminal() => StaggeredCombination(false);
        private IEnumerator StaggeredCombination(bool flexFirst)
        {
            var frames = new List<EmgSample>();
            for (int i = 0; i <= 400; i++) frames.Add(new EmgSample(i, i * .005,
                i >= (flexFirst ? 50 : 100) ? .8f : 0, i >= (flexFirst ? 100 : 50) ? .8f : 0));
            var source = new ReplayEmgSource(frames, () => Time.realtimeSinceStartupAsDouble);
            driver.SetSource(source, new EmgCalibration(source.Info.Id));
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(terminal.IsOpen, Is.False);
            Assert.That(terminal.Challenge.Progress, Is.GreaterThan(0));
            Assert.That(motor.JumpCount, Is.Zero);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(terminal.IsOpen, Is.True); Assert.That(gate.enabled, Is.False);
            Assert.That(motor.IsAtLock, Is.True); Assert.That(motor.JumpCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator CombinationOpensGateAndTrailingExtendDoesNotJump()
        {
            Replay(true); yield return new WaitForSecondsRealtime(.95f);
            Assert.That(terminal.IsOpen,Is.True); Assert.That(gate.enabled,Is.False);
            Assert.That(motor.IsAtLock,Is.True); Assert.That(motor.JumpCount,Is.Zero);
            yield return new WaitForSecondsRealtime(.2f); // Only Extend remains after Flex drops.
            Assert.That(motor.IsAtLock,Is.True); Assert.That(motor.JumpCount,Is.Zero);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(motor.IsAtLock,Is.False); Assert.That(motor.Body.position.x,Is.GreaterThan(1.8f));
        }
        [UnityTest]
        public IEnumerator SingleChannelCannotOpenOrJumpInsideTerminal()
        {
            Replay(false); yield return new WaitForSecondsRealtime(.9f);
            Assert.That(motor.IsAtLock,Is.True); Assert.That(terminal.IsOpen,Is.False);
            Assert.That(motor.JumpCount,Is.Zero); Assert.That(gate.enabled,Is.True);
        }
        [UnityTest]
        public IEnumerator ResetClosesGateAndClearsTerminalContext()
        {
            Replay(true); yield return new WaitForSecondsRealtime(.95f); Assert.That(terminal.IsOpen,Is.True);
            motor.ResetTo(new Vector2(-3,.82f)); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(terminal.IsOpen,Is.False); Assert.That(gate.enabled,Is.True); Assert.That(controller.IsEngaged,Is.False);
        }
    }
}
