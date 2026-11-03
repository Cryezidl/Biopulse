using System.Collections;
using System.Collections.Generic;
using Biopulse.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Biopulse.Tests
{
    public sealed class RunnerMotorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private PhysicsMaterial2D material;
        [SetUp]
        public void Setup() => material = new PhysicsMaterial2D { friction = 0, bounciness = 0 };
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear(); Object.Destroy(material);
            yield return null;
        }
        private BoxCollider2D Box(string name, Vector2 position, Vector2 size, bool trigger = false)
        {
            var go = new GameObject(name); objects.Add(go); go.layer = 31; go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
            collider.sharedMaterial = material; collider.isTrigger = trigger; return collider;
        }
        private RunnerMotor2D Runner(Vector2 position, float speed = 4)
        {
            var go = new GameObject("Test runner"); objects.Add(go); go.layer = 31; go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 3;
            // Isolate fixture bodies from the currently open scene without changing project settings.
            body.excludeLayers = ~(1 << 31);
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(.8f, 1.6f); collider.sharedMaterial = material;
            var motor = go.AddComponent<RunnerMotor2D>(); motor.RunSpeed = speed; return motor;
        }

        [UnityTest]
        public IEnumerator FallsLandsAndAutorunsWithoutSinkingOrRotating()
        {
            Box("Floor", new Vector2(10, -.5f), new Vector2(40, 1));
            var motor = Runner(new Vector2(0, 3));
            yield return new WaitForSeconds(.8f);
            Assert.That(motor.Body.position.x, Is.GreaterThan(2.5f));
            Assert.That(motor.Body.position.y, Is.EqualTo(.8f).Within(.04f));
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.IsBlocked, Is.False);
            Assert.That(motor.Body.rotation, Is.EqualTo(0).Within(.001));
        }

        [UnityTest]
        public IEnumerator WallStopsRunnerButDoesNotCountAsGround()
        {
            Box("Wall", new Vector2(2, 2), new Vector2(.2f, 6));
            var motor = Runner(new Vector2(0, 2)); motor.Body.gravityScale = 0;
            yield return new WaitForSeconds(.7f);
            Assert.That(motor.IsBlocked, Is.True);
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(motor.Body.position.x, Is.LessThan(1.53f));
            Assert.That(motor.Body.linearVelocity.x, Is.EqualTo(0).Within(.01));
        }

        [UnityTest]
        public IEnumerator TriggerCannotSupportRunner()
        {
            Box("Trigger", new Vector2(0, 0), new Vector2(20, .2f), true);
            var motor = Runner(new Vector2(0, 2), 0);
            yield return new WaitForSeconds(.6f);
            Assert.That(motor.Body.position.y, Is.LessThan(0));
            Assert.That(motor.IsGrounded, Is.False);
        }

        [UnityTest]
        public IEnumerator ContinuousCollisionStopsFastRunnerAtThinWall()
        {
            Box("Thin wall", new Vector2(3, 2), new Vector2(.05f, 5));
            var motor = Runner(new Vector2(0, 2), 40); motor.Body.gravityScale = 0;
            yield return new WaitForSeconds(.3f);
            Assert.That(motor.Body.position.x, Is.LessThan(2.7f));
            Assert.That(motor.IsBlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator SuspensionFreezesFallAndResumePreservesVerticalMotion()
        {
            var motor = Runner(new Vector2(0, 6));
            yield return new WaitForSeconds(.1f);
            var position = motor.Body.position;
            float vertical = motor.Body.linearVelocity.y;
            motor.SetSuspended(true);
            yield return new WaitForSeconds(.15f);
            Assert.That(motor.Body.position, Is.EqualTo(position));
            motor.SetSuspended(false);
            Assert.That(motor.Body.linearVelocity.y, Is.EqualTo(vertical));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(motor.Body.position.y, Is.LessThan(position.y));
        }

        [UnityTest]
        public IEnumerator LeavingPlatformFallsAndResetClearsOldContact()
        {
            Box("Short floor", new Vector2(0, -.5f), new Vector2(2, 1));
            var motor = Runner(new Vector2(0, .82f));
            yield return new WaitForSeconds(.6f);
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(motor.Body.position.y, Is.LessThan(.7f));
            motor.ResetTo(new Vector2(-2, 3));
            Assert.That(motor.Body.position, Is.EqualTo(new Vector2(-2, 3)));
            Assert.That(motor.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(motor.IsBlocked, Is.False);
        }
    }
}
